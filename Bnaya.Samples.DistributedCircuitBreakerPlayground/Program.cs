using Polly;
using Polly.CircuitBreaker;

Console.WriteLine("Starting Distributed Circuit Breaker Playground");

// Create combined retry and circuit breaker policy
var retryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            Console.WriteLine($"Retry {retryCount} after {timespan} seconds");
        });

var circuitBreakerPolicy = Policy
    .Handle<Exception>()
    .AdvancedCircuitBreakerAsync(failureThreshold: 0.8,
                                 samplingDuration: TimeSpan.FromSeconds(3),
                                 minimumThroughput: 10,
                                 durationOfBreak: TimeSpan.FromSeconds(30),
                                 onBreak: (Exception ex, TimeSpan duration) =>
                                 {
                                     Console.WriteLine("Break");
                                 },
                                 () =>
                                 {
                                     Console.WriteLine("Reset");
                                 },
                                 () =>
                                 {
                                    Console.WriteLine("Half Open");
                                 });

var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

// Execute the policy

try
{
    await combinedPolicy.ExecuteAsync(DOAsync);
}
catch (Exception ex)
{
    Console.WriteLine($"Operation failed: {ex.Message}");
}

Console.WriteLine("Application completed");

static async Task DOAsync()
{
    Console.WriteLine("DOAsync completed successfully");
}

