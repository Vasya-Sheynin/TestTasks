---
title: "NotificationService API"
version: "1.0.0"
date: "2025-07-29"
---

# Overview
The `NotificationService` provides a unified interface for sending various types of notifications including emails, SMS messages, and push notifications. It integrates with SMTP servers for email, Twilio for SMS, and Firebase Cloud Messaging (FCM) for push notifications.

# API Reference

## Interfaces
| Interface                     | Description                                                                 |
|-------------------------------|-----------------------------------------------------------------------------|
| `INotificationService`        | Defines the contract for notification services                              |

## Methods
| Method                                           | Description                                        | Return Type                   |
|--------------------------------------------------|----------------------------------------------------|-------------------------------|
| `SendEmailAsync(to, subject, body)`             | Sends an email to the specified recipient         | `Task<NotificationResult>`    |
| `SendSmsAsync(phoneNumber, message)`            | Sends an SMS to the specified phone number        | `Task<NotificationResult>`    |
| `SendPushAsync(deviceToken, title, body)`       | Sends a push notification to the specified device | `Task<NotificationResult>`    |

# Examples

## C# Usage
```csharp
// Initialize the service (typically done via DI)
var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<NotificationService>();
var smtpSettings = new SmtpSettings { /* ... */ };
var twilioSettings = new TwilioSettings { /* ... */ };
var firebase = FirebaseMessaging.DefaultInstance;

var notificationService = new NotificationService(
    logger,
    smtpSettings,
    twilioSettings,
    firebase);

// Send an email
var emailResult = await notificationService.SendEmailAsync(
    "user@example.com",
    "Welcome",
    "Thank you for registering!");

// Send an SMS
var smsResult = await notificationService.SendSmsAsync(
    "+1234567890",
    "Your verification code is 123456");

// Send a push notification
var pushResult = await notificationService.SendPushAsync(
    "device-token-abc123",
    "New Message",
    "You have a new notification");
```

## Configuration Requirements
```json
// SMTP Settings (appsettings.json example)
"SmtpSettings": {
  "Host": "smtp.example.com",
  "Port": 587,
  "Username": "user@example.com",
  "Password": "your-password",
  "From": "noreply@example.com"
}

// Twilio Settings (appsettings.json example)
"TwilioSettings": {
  "AccountSid": "your-account-sid",
  "AuthToken": "your-auth-token",
  "FromNumber": "+1987654321"
}
```

# Error Codes
| Code              | Description                          |
|-------------------|--------------------------------------|
| EmailError        | Failed to send email                 |
| SmsError          | Failed to send SMS                   |
| PushError         | Failed to send push notification     |

# Models

## NotificationResult
Represents the result of a notification operation.

Properties:
- `IsSuccess` (bool): Indicates if the operation succeeded
- `ErrorCode` (string): Machine-readable error code (null if success)
- `ErrorMessage` (string): Human-readable error message (null if success)

## Settings Models

### SmtpSettings
Configuration for SMTP email service.

Properties:
- `Host` (string): SMTP server hostname
- `Port` (int): SMTP server port
- `Username` (string): Authentication username
- `Password` (string): Authentication password
- `From` (string): Sender email address

### TwilioSettings
Configuration for Twilio SMS service.

Properties:
- `AccountSid` (string): Twilio account SID
- `AuthToken` (string): Twilio auth token
- `FromNumber` (string): Twilio phone number

# Implementation Notes
1. The service implements proper error handling and logging for all operations
2. All methods are asynchronous and return `Task<NotificationResult>`
3. Twilio client is initialized once during service construction
4. Firebase Messaging instance should be configured before injection
5. SMTP client is created per-email with SSL enabled by default

# Dependencies
- `System.Net.Mail` (for email)
- `Twilio` (for SMS)
- `FirebaseAdmin.Messaging` (for push notifications)
- `Microsoft.Extensions.Logging` (for logging)
