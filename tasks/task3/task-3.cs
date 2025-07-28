//  ISSUE: The PaymentProcessingService intermittently throws a NullReferenceException when deserializing the 
//      gateway’s JSON response (which can be empty or malformed), and lacks any resilience logic (e.g. retries or circuit breaker), 
//      leading to unhandled errors at runtime.

//  TASK: Write a prompt for an LLM to fix an error.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Task3.Models;
using System.Threading;

namespace Task3.Models
{
    public class Account
    {
        public int Id { get; set; }
        public decimal Balance { get; set; }
    }

    public class PaymentResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public decimal? NewBalance { get; set; }

        public static PaymentResult Success(decimal newBalance)
        {
            return new PaymentResult
            {
                IsSuccess = true,
                NewBalance = newBalance
            };
        }

        public static PaymentResult Failed(string errorMessage)
        {
            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}

namespace Task3.Services
{
    public class GatewayResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public interface IPaymentProcessingService
    {
        Task<PaymentResult> ProcessPaymentAsync(int accountId, decimal amount);
    }

    public class PaymentProcessingService : IPaymentProcessingService
    {
        private readonly ILogger<PaymentProcessingService> _logger;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<int, Account> _accounts = new Dictionary<int, Account>();

        // Circuit breaker state
        private int _consecutiveFailures = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private readonly int _maxConsecutiveFailures = 3;
        private readonly TimeSpan _circuitBreakerTimeout = TimeSpan.FromMinutes(1);

        // Retry configuration
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(1);

        public PaymentProcessingService(ILogger<PaymentProcessingService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;

            _accounts[1] = new Account { Id = 1, Balance = 5000m };
            _accounts[2] = new Account { Id = 2, Balance = 0m };
            // ...
        }

        public async Task<PaymentResult> ProcessPaymentAsync(int accountId, decimal amount)
        {
            _logger.LogInformation("Start processing payment for account {AccountId}, amount {Amount}", accountId, amount);

            try
            {
                // 1) Check cache
                if (!_accounts.TryGetValue(accountId, out var account))
                {
                    _logger.LogError("Account {AccountId} not found in cache", accountId);
                    return PaymentResult.Failed("Account not found");
                }

                // 2) Check circuit breaker
                if (IsCircuitBreakerOpen())
                {
                    _logger.LogWarning("Circuit breaker is open, rejecting payment request for account {AccountId}", accountId);
                    return PaymentResult.Failed("Service temporarily unavailable");
                }

                // 3) Send request to payment service with retry logic
                var gatewayResult = await SendPaymentRequestWithRetryAsync(account, amount);
                if (gatewayResult == null)
                {
                    return PaymentResult.Failed("Gateway communication failed");
                }

                if (!gatewayResult.Success)
                {
                    _logger.LogWarning("Payment gateway failed: {Message}", gatewayResult.Message);
                    return PaymentResult.Failed("Gateway failure: " + gatewayResult.Message);
                }

                // 4) Update balance locally
                var balanceResult = UpdateAccountBalance(account, amount);
                if (!balanceResult.IsSuccess)
                {
                    return balanceResult;
                }

                _logger.LogInformation("Payment successful. New balance: {Balance}", account.Balance);
                return PaymentResult.Success(account.Balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing payment for account {AccountId}", accountId);
                RecordFailure();
                return PaymentResult.Failed("Internal service error");
            }
        }

        private async Task<GatewayResponse> SendPaymentRequestWithRetryAsync(Account account, decimal amount)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Attempt {Attempt} of {MaxRetries} to send payment request", attempt, _maxRetries);

                    var request = new
                    {
                        AccountId = account.Id,
                        Amount = amount
                    };

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
                    var response = await _httpClient.PostAsync(
                        "https://api.payment-gateway.com/v1/payments",
                        new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"),
                        cts.Token
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Payment gateway returned status {StatusCode} on attempt {Attempt}", response.StatusCode, attempt);

                        if (attempt == _maxRetries)
                        {
                            RecordFailure();
                            return null;
                        }

                        await Task.Delay(_retryDelay * attempt); // Exponential backoff
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Gateway response: {Response}", json);

                    // Validate JSON response
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogWarning("Empty response from gateway on attempt {Attempt}", attempt);

                        if (attempt == _maxRetries)
                        {
                            RecordFailure();
                            return null;
                        }

                        await Task.Delay(_retryDelay * attempt);
                        continue;
                    }

                    // Safe JSON deserialization
                    GatewayResponse gatewayResult;
                    try
                    {
                        gatewayResult = JsonSerializer.Deserialize<GatewayResponse>(json);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize gateway response on attempt {Attempt}: {Json}", attempt, json);

                        if (attempt == _maxRetries)
                        {
                            RecordFailure();
                            return null;
                        }

                        await Task.Delay(_retryDelay * attempt);
                        continue;
                    }

                    // Validate deserialized result
                    if (gatewayResult == null)
                    {
                        _logger.LogWarning("Gateway response deserialized to null on attempt {Attempt}", attempt);

                        if (attempt == _maxRetries)
                        {
                            RecordFailure();
                            return null;
                        }

                        await Task.Delay(_retryDelay * attempt);
                        continue;
                    }

                    // Success - reset failure counter
                    ResetFailureCounter();
                    return gatewayResult;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "HTTP request failed on attempt {Attempt}", attempt);

                    if (attempt == _maxRetries)
                    {
                        RecordFailure();
                        return null;
                    }

                    await Task.Delay(_retryDelay * attempt);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(ex, "Request timeout on attempt {Attempt}", attempt);

                    if (attempt == _maxRetries)
                    {
                        RecordFailure();
                        return null;
                    }

                    await Task.Delay(_retryDelay * attempt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error on attempt {Attempt}", attempt);

                    if (attempt == _maxRetries)
                    {
                        RecordFailure();
                        return null;
                    }

                    await Task.Delay(_retryDelay * attempt);
                }
            }

            return null;
        }

        private PaymentResult UpdateAccountBalance(Account account, decimal amount)
        {
            if (amount < 0)
            {
                _logger.LogWarning("Negative payment amount: {Amount}", amount);
                return PaymentResult.Failed("Amount must be non-negative");
            }

            if (account.Balance < amount)
            {
                _logger.LogWarning("Insufficient funds for account {AccountId}", account.Id);
                return PaymentResult.Failed("Insufficient funds");
            }

            account.Balance -= amount;
            return PaymentResult.Success(account.Balance);
        }

        private bool IsCircuitBreakerOpen()
        {
            if (_consecutiveFailures >= _maxConsecutiveFailures)
            {
                var timeSinceLastFailure = DateTime.UtcNow - _lastFailureTime;
                if (timeSinceLastFailure < _circuitBreakerTimeout)
                {
                    _logger.LogWarning("Circuit breaker is open. Failures: {Failures}, Time since last failure: {TimeSinceLastFailure}",
                        _consecutiveFailures, timeSinceLastFailure);
                    return true;
                }
                else
                {
                    _logger.LogInformation("Circuit breaker timeout expired, attempting to close");
                    ResetFailureCounter();
                }
            }
            return false;
        }

        private void RecordFailure()
        {
            _consecutiveFailures++;
            _lastFailureTime = DateTime.UtcNow;
            _logger.LogWarning("Recorded failure. Consecutive failures: {Failures}", _consecutiveFailures);
        }

        private void ResetFailureCounter()
        {
            _consecutiveFailures = 0;
            _lastFailureTime = DateTime.MinValue;
            _logger.LogDebug("Reset failure counter");
        }
    }
}

//LOGS
// 2025-06-30 11:15:42.001 +02:00 [INF] MyApp.Services.PaymentProcessingService 
//     Start processing payment for account 5, amount 120.00
// 2025-06-30 11:15:42.105 +02:00 [ERR] MyApp.Services.PaymentProcessingService 
//     Object reference not set to an instance of an object.
//    at MyApp.Services.PaymentProcessingService.ProcessPaymentAsync(Int32 accountId, Decimal amount) in /src/Services/PaymentProcessingService.cs:line  thirty-some
// ...
// 2025-06-30 11:16:10.250 +02:00 [INFO] Request to ProcessPaymentAsync for account 5
// 2025-06-30 11:16:10.352 +02:00 [ERR] MyApp.Services.PaymentProcessingService 
//     System.Collections.Generic.KeyNotFoundException: The given key was not present in the dictionary.

