using FluentAssertions;
using Squish.Core.Services;
using Xunit;

namespace Squish.Core.Tests.Services;

public class InMemoryLoggerTests
{
    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        // Act
        var logger = new InMemoryLogger();

        // Assert
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LogWarning_AddsMessageToWarnings()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var warningMessage = "Test warning message";

        // Act
        logger.LogWarning(warningMessage);

        // Assert
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be(warningMessage);
        logger.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LogError_AddsMessageToErrors()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var errorMessage = "Test error message";

        // Act
        logger.LogError(errorMessage);

        // Assert
        logger.Errors.Should().ContainSingle()
            .Which.Should().Be(errorMessage);
        logger.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void LogWarning_MultipleMessages_AddsAllMessages()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var warnings = new[] { "Warning 1", "Warning 2", "Warning 3" };

        // Act
        foreach (var warning in warnings)
        {
            logger.LogWarning(warning);
        }

        // Assert
        logger.Warnings.Should().HaveCount(3);
        logger.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void LogError_MultipleMessages_AddsAllMessages()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var errors = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        foreach (var error in errors)
        {
            logger.LogError(error);
        }

        // Assert
        logger.Errors.Should().HaveCount(3);
        logger.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void LogWarning_AndLogError_BothCollectionsPopulated()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var warningMessage = "Test warning";
        var errorMessage = "Test error";

        // Act
        logger.LogWarning(warningMessage);
        logger.LogError(errorMessage);

        // Assert
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be(warningMessage);
        logger.Errors.Should().ContainSingle()
            .Which.Should().Be(errorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void LogWarning_HandlesEmptyOrNullMessages(string? message)
    {
        // Arrange
        var logger = new InMemoryLogger();

        // Act
        logger.LogWarning(message!);

        // Assert
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be(message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void LogError_HandlesEmptyOrNullMessages(string? message)
    {
        // Arrange
        var logger = new InMemoryLogger();

        // Act
        logger.LogError(message!);

        // Assert
        logger.Errors.Should().ContainSingle()
            .Which.Should().Be(message);
    }

    [Fact]
    public void Warnings_ReturnsReadOnlyCollection()
    {
        // Arrange
        var logger = new InMemoryLogger();
        logger.LogWarning("Test warning");

        // Act
        var warnings = logger.Warnings;

        // Assert
        warnings.Should().BeAssignableTo<IReadOnlyList<string>>();
        warnings.Should().HaveCount(1);
    }

    [Fact]
    public void Errors_ReturnsReadOnlyCollection()
    {
        // Arrange
        var logger = new InMemoryLogger();
        logger.LogError("Test error");

        // Act
        var errors = logger.Errors;

        // Assert
        errors.Should().BeAssignableTo<IReadOnlyList<string>>();
        errors.Should().HaveCount(1);
    }

    [Fact]
    public void Warnings_ReturnsSnapshot_NotLiveCollection()
    {
        // Arrange
        var logger = new InMemoryLogger();
        logger.LogWarning("Initial warning");

        // Act
        var initialWarnings = logger.Warnings;
        logger.LogWarning("New warning");
        var updatedWarnings = logger.Warnings;

        // Assert
        initialWarnings.Should().HaveCount(1);
        updatedWarnings.Should().HaveCount(2);
        initialWarnings.Should().NotBeSameAs(updatedWarnings);
    }

    [Fact]
    public void Errors_ReturnsSnapshot_NotLiveCollection()
    {
        // Arrange
        var logger = new InMemoryLogger();
        logger.LogError("Initial error");

        // Act
        var initialErrors = logger.Errors;
        logger.LogError("New error");
        var updatedErrors = logger.Errors;

        // Assert
        initialErrors.Should().HaveCount(1);
        updatedErrors.Should().HaveCount(2);
        initialErrors.Should().NotBeSameAs(updatedErrors);
    }

    [Fact]
    public void LogWarning_PreservesMessageOrder()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var messages = new[] { "First", "Second", "Third", "Fourth" };

        // Act
        foreach (var message in messages)
        {
            logger.LogWarning(message);
        }

        // Assert
        logger.Warnings.Should().Equal(messages);
    }

    [Fact]
    public void LogError_PreservesMessageOrder()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var messages = new[] { "First", "Second", "Third", "Fourth" };

        // Act
        foreach (var message in messages)
        {
            logger.LogError(message);
        }

        // Assert
        logger.Errors.Should().Equal(messages);
    }

    [Fact]
    public async Task LogWarning_ThreadSafety_ConcurrentWrites()
    {
        // Arrange
        var logger = new InMemoryLogger();
        const int threadCount = 10;
        const int messagesPerThread = 100;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < messagesPerThread; j++)
                {
                    logger.LogWarning($"Thread{threadIndex}_Message{j}");
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        logger.Warnings.Should().HaveCount(threadCount * messagesPerThread);
        
        // Verify all messages are present (order may vary due to concurrency)
        for (int i = 0; i < threadCount; i++)
        {
            for (int j = 0; j < messagesPerThread; j++)
            {
                logger.Warnings.Should().Contain($"Thread{i}_Message{j}");
            }
        }
    }

    [Fact]
    public async Task LogError_ThreadSafety_ConcurrentWrites()
    {
        // Arrange
        var logger = new InMemoryLogger();
        const int threadCount = 10;
        const int messagesPerThread = 100;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < messagesPerThread; j++)
                {
                    logger.LogError($"Thread{threadIndex}_Error{j}");
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        logger.Errors.Should().HaveCount(threadCount * messagesPerThread);
        
        // Verify all messages are present
        for (int i = 0; i < threadCount; i++)
        {
            for (int j = 0; j < messagesPerThread; j++)
            {
                logger.Errors.Should().Contain($"Thread{i}_Error{j}");
            }
        }
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentReadWriteOperations()
    {
        // Arrange
        var logger = new InMemoryLogger();
        const int writerThreads = 5;
        const int readerThreads = 3;
        const int messagesPerWriter = 50;
        var tasks = new List<Task>();

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act - Start writer threads
        for (int i = 0; i < writerThreads; i++)
        {
            int writerIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < messagesPerWriter; j++)
                {
                    logger.LogWarning($"Writer{writerIndex}_Warning{j}");
                    logger.LogError($"Writer{writerIndex}_Error{j}");
                    await Task.Delay(1); // Small delay to encourage concurrency
                }
            }));
        }

        // Start reader threads
        for (int i = 0; i < readerThreads; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var warnings = logger.Warnings; // Read operation
                    var errors = logger.Errors; // Read operation
                    
                    // These should not throw or cause corruption
                    _ = warnings.Count;
                    _ = errors.Count;
                }
            }));
        }

        await Task.WhenAll(tasks.Take(writerThreads)); // Wait for writers to complete
        cancellationTokenSource.Cancel(); // Stop readers
        
        try
        {
            await Task.WhenAll(tasks); // Wait for all tasks (readers will be cancelled)
        }
        catch (OperationCanceledException)
        {
            // Expected for reader tasks
        }

        // Assert
        logger.Warnings.Should().HaveCount(writerThreads * messagesPerWriter);
        logger.Errors.Should().HaveCount(writerThreads * messagesPerWriter);
    }

    [Fact]
    public void LogWarning_VeryLongMessage_HandledCorrectly()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var longMessage = new string('A', 10000); // Very long message

        // Act
        logger.LogWarning(longMessage);

        // Assert
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be(longMessage);
    }

    [Fact]
    public void LogError_VeryLongMessage_HandledCorrectly()
    {
        // Arrange
        var logger = new InMemoryLogger();
        var longMessage = new string('X', 10000); // Very long message

        // Act
        logger.LogError(longMessage);

        // Assert
        logger.Errors.Should().ContainSingle()
            .Which.Should().Be(longMessage);
    }
}