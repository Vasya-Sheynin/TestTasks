using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
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
        private readonly TimeSpan _initialRetryDelay = TimeSpan.FromSeconds(1);
        private readonly double _retryBackoffFactor = 1.5;

        public PaymentProcessingService(ILogger<PaymentProcessingService> logger, HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Initialize with some sample accounts
            _accounts[1] = new Account { Id = 1, Balance = 5000m };
            _accounts[2] = new Account { Id = 2, Balance = 0m };
        }

        public async Task<PaymentResult> ProcessPaymentAsync(int accountId, decimal amount)
        {
            if (amount <= 0)
            {
                _logger.LogWarning("Invalid amount {Amount} for account {AccountId}", amount, accountId);
                return PaymentResult.Failed("Amount must be positive");
            }

            _logger.LogInformation("Start processing payment for account {AccountId}, amount {Amount}", accountId, amount);

            try
            {
                // Check account existence
                if (!_accounts.TryGetValue(accountId, out var account))
                {
                    _logger.LogError("Account {AccountId} not found", accountId);
                    return PaymentResult.Failed($"Account {accountId} not found");
                }

                // Check circuit breaker
                if (IsCircuitBreakerOpen())
                {
                    _logger.LogWarning("Circuit breaker is open, rejecting payment request");
                    return PaymentResult.Failed("Service temporarily unavailable. Please try again later.");
                }

                // Process payment with retries
                var gatewayResult = await ProcessPaymentWithRetriesAsync(account, amount);
                if (gatewayResult == null)
                {
                    return PaymentResult.Failed("Payment service unavailable after multiple attempts");
                }

                if (!gatewayResult.Success)
                {
                    var errorMessage = string.IsNullOrEmpty(gatewayResult.Message) 
                        ? "Payment failed" 
                        : gatewayResult.Message;
                    return PaymentResult.Failed(errorMessage);
                }

                // Update balance
                return UpdateAccountBalance(account, amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing payment for account {AccountId}", accountId);
                RecordFailure();
                return PaymentResult.Failed("An unexpected error occurred");
            }
        }

        private async Task<GatewayResponse> ProcessPaymentWithRetriesAsync(Account account, decimal amount)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Payment attempt {Attempt}/{MaxRetries}", attempt, _maxRetries);

                    var result = await TryProcessPaymentAsync(account, amount);
                    if (result != null)
                    {
                        ResetFailureCounter();
                        return result;
                    }

                    if (attempt == _maxRetries)
                    {
                        RecordFailure();
                        return null;
                    }

                    var delay = CalculateRetryDelay(attempt);
                    _logger.LogDebug("Waiting {Delay} before next attempt", delay);
                    await Task.Delay(delay);
                }
                catch (Exception ex) when (attempt == _maxRetries)
                {
                    _logger.LogError(ex, "Final payment attempt failed");
                    RecordFailure();
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Payment attempt {Attempt} failed", attempt);
                    await Task.Delay(CalculateRetryDelay(attempt));
                }
            }

            return null;
        }

        private async Task<GatewayResponse> TryProcessPaymentAsync(Account account, decimal amount)
        {
            try
            {
                var request = new
                {
                    AccountId = account.Id,
                    Amount = amount
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.PostAsync(
                    "https://api.payment-gateway.com/v1/payments",
                    new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"),
                    cts.Token
                );

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Empty response from payment gateway");
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<GatewayResponse>(json, options);
                if (result == null)
                {
                    _logger.LogWarning("Failed to deserialize gateway response");
                    return null;
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP request failed");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON deserialization failed");
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Request timed out");
                throw;
            }
        }

        private TimeSpan CalculateRetryDelay(int attempt)
        {
            var delay = _initialRetryDelay.TotalMilliseconds * Math.Pow(_retryBackoffFactor, attempt - 1);
            return TimeSpan.FromMilliseconds(delay);
        }

        private PaymentResult UpdateAccountBalance(Account account, decimal amount)
        {
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
                    _logger.LogWarning("Circuit breaker is open (failures: {Failures}, time since last failure: {Time})",
                        _consecutiveFailures, timeSinceLastFailure);
                    return true;
                }

                _logger.LogInformation("Circuit breaker timeout expired, resetting");
                ResetFailureCounter();
            }
            return false;
        }

        private void RecordFailure()
        {
            _consecutiveFailures++;
            _lastFailureTime = DateTime.UtcNow;
            _logger.LogWarning("Recorded failure (total: {Failures})", _consecutiveFailures);
        }

        private void ResetFailureCounter()
        {
            _consecutiveFailures = 0;
            _lastFailureTime = DateTime.MinValue;
            _logger.LogDebug("Reset failure counter");
        }
    }
}