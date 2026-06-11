using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using PracticalWork.Library.Email;
using PracticalWork.Library.Models;

namespace PracticalWork.Email.Tests;

public class EmailServiceTests
{
    private readonly Mock<ISmtpClient> _smtpClientMock;
    private readonly EmailService _emailService;

    private readonly EmailOptions _options = new()
    {
        SenderName  = "Тестовая библиотека",
        SenderEmail = "noreply@test.local",
        SmtpServer  = "localhost",
        SmtpPort    = 25,
        UseSsl      = false
    };

    public EmailServiceTests()
    {
        _smtpClientMock = new Mock<ISmtpClient>();

        SetupDisconnectedClient();

        _emailService = new EmailService(
            _smtpClientMock.Object,
            Options.Create(_options));
    }
    
    private void SetupDisconnectedClient()
    {
        _smtpClientMock
            .Setup(c => c.IsConnected)
            .Returns(false);

        _smtpClientMock
            .Setup(c => c.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _smtpClientMock
            .Setup(c => c.AuthenticationMechanisms)
            .Returns(new HashSet<string>());
    }
    
    private void SetupConnectedClient()
    {
        _smtpClientMock
            .Setup(c => c.IsConnected)
            .Returns(true);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpSucceeds_ReturnsIsSuccessTrue()
    {
        // Arrange
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "<p>Тело</p>",
            IsHtml  = true
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ReturnsAsync("OK");

        // Act
        var result = await _emailService.SendAsync(message);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpSucceeds_ResultMessageEqualsSmtpResponse()
    {
        // Arrange
        const string smtpResponse = "2.0.0 OK message accepted";
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ReturnsAsync(smtpResponse);

        // Act
        var result = await _emailService.SendAsync(message);

        // Assert
        Assert.Equal(smtpResponse, result.Message);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpThrows_ReturnsIsSuccessFalse()
    {
        // Arrange
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ThrowsAsync(new InvalidOperationException("Соединение отклонено"));

        // Act
        var result = await _emailService.SendAsync(message);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpThrows_ResultMessageContainsExceptionMessage()
    {
        // Arrange
        const string errorText = "Соединение с SMTP сервером потеряно";
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ThrowsAsync(new InvalidOperationException(errorText));

        // Act
        var result = await _emailService.SendAsync(message);

        // Assert
        Assert.Contains(errorText, result.Message);
    }

    [Fact]
    public async Task SendAsync_NeverThrowsException_ReturnsResultInstead()
    {
        // Arrange
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ThrowsAsync(new Exception("Неожиданная ошибка SMTP"));

        // Act
        var ex = await Record.ExceptionAsync(() => _emailService.SendAsync(message));

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public async Task SendAsync_WhenIsHtmlTrue_SendsMimeMessageWithHtmlBody()
    {
        // Arrange
        const string htmlBody = "<h1>Заголовок</h1><p>Текст</p>";
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "HTML письмо",
            Body    = htmlBody,
            IsHtml  = true
        };

        MimeMessage? capturedMime = null;
        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>(
                (m, _, _) => capturedMime = m)
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        Assert.NotNull(capturedMime);
        Assert.Contains("h1", capturedMime.HtmlBody ?? capturedMime.Body?.ToString() ?? "");
    }

    [Fact]
    public async Task SendAsync_WhenIsHtmlFalse_SendsMimeMessageWithTextBody()
    {
        // Arrange
        const string textBody = "Простой текст без HTML";
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Текстовое письмо",
            Body    = textBody,
            IsHtml  = false
        };

        MimeMessage? capturedMime = null;
        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>(
                (m, _, _) => capturedMime = m)
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        Assert.NotNull(capturedMime);
        Assert.Contains(textBody, capturedMime.TextBody);
    }

    [Fact]
    public async Task SendAsync_SetsFromAddressFromOptions()
    {
        // Arrange
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        MimeMessage? capturedMime = null;
        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>(
                (m, _, _) => capturedMime = m)
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        Assert.NotNull(capturedMime);
        var fromAddress = capturedMime.From.Mailboxes.FirstOrDefault();
        Assert.NotNull(fromAddress);
        Assert.Equal(_options.SenderEmail, fromAddress.Address);
        Assert.Equal(_options.SenderName,  fromAddress.Name);
    }

    [Fact]
    public async Task SendAsync_SetsToAddressFromMessage()
    {
        // Arrange
        const string recipientEmail = "specific.reader@library.com";
        var message = new EmailMessage
        {
            EmailTo = recipientEmail,
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        MimeMessage? capturedMime = null;
        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>(
                (m, _, _) => capturedMime = m)
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        Assert.NotNull(capturedMime);
        var toAddress = capturedMime.To.Mailboxes.FirstOrDefault();
        Assert.NotNull(toAddress);
        Assert.Equal(recipientEmail, toAddress.Address);
    }

    [Fact]
    public async Task SendAsync_SetsSubjectFromMessage()
    {
        // Arrange
        const string subject = "Напоминание о возврате книги: «Война и мир»";
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = subject,
            Body    = "Текст",
            IsHtml  = false
        };

        MimeMessage? capturedMime = null;
        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>(
                (m, _, _) => capturedMime = m)
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        Assert.NotNull(capturedMime);
        Assert.Equal(subject, capturedMime.Subject);
    }

    [Fact]
    public async Task SendAsync_WhenRecipientNameProvided_SetsDisplayNameInMimeMessage()
    {
        // Arrange
        var message = new EmailMessage
        {
            EmailTo       = "reader@test.com",
            RecipientName = "Иван Иванов",
            Subject       = "Тест",
            Body          = "Текст",
            IsHtml        = false
        };

        MimeMessage? capturedMime = null;
        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>(
                (m, _, _) => capturedMime = m)
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        Assert.NotNull(capturedMime);
        var toAddress = capturedMime.To.Mailboxes.FirstOrDefault();
        Assert.NotNull(toAddress);
        Assert.Equal("Иван Иванов", toAddress.Name);
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_CallsConnectAsyncBeforeSend()
    {
        // Arrange
        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        _smtpClientMock.Verify(
            c => c.ConnectAsync(
                _options.SmtpServer,
                _options.SmtpPort,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenAlreadyConnected_DoesNotCallConnectAsync()
    {
        // Arrange
        SetupConnectedClient();

        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        _smtpClientMock
            .Setup(c => c.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<ITransferProgress>()))
            .ReturnsAsync("OK");

        // Act
        await _emailService.SendAsync(message);

        // Assert
        _smtpClientMock.Verify(
            c => c.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenConnectFails_ReturnsIsSuccessFalse()
    {
        // Arrange
        _smtpClientMock
            .Setup(c => c.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Хост недоступен"));

        var message = new EmailMessage
        {
            EmailTo = "reader@test.com",
            Subject = "Тест",
            Body    = "Текст",
            IsHtml  = false
        };

        // Act
        var result = await _emailService.SendAsync(message);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Хост недоступен", result.Message);
    }
}