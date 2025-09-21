using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy;

// 2% of all requests will be injected with chaos fault.
const double FAULT_INJECTION_RATE = 0.30;
// inject a latency.
const double LATENCY_INJECTION_RATE = 0.50;
// inject with outcome
const double OUTCOME_INJECTION_RATE = 0.10;
// injected a custom behavior.
const double BEHAVIOR_INJECTION_RATE = 0.20;

Console.WriteLine("Starting Distributed Circuit Breaker Playground");

var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
// First, configure regular resilience strategies
builder
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage> 
            {
                MaxRetryAttempts = 2,                
                Delay = TimeSpan.FromMilliseconds(10), 
                BackoffType = DelayBackoffType.Constant, 
                OnRetry = (args) => 
                {
                    Console.WriteLine("Retry");
                    return ValueTask.CompletedTask;
                }
            })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage> 
                {
                    BreakDuration = TimeSpan.FromSeconds(4),
                    FailureRatio = 0.2,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(1),OnOpened = (args) => 
                    {
                        Console.WriteLine("Circuit Breaker - On Opened");
                        return ValueTask.CompletedTask;
                    },
                    Name = "PlaygroundCircuitBreaker",                    
                    OnClosed = (args) => 
                    {
                        Console.WriteLine("Circuit Breaker - On Closed");
                        return ValueTask.CompletedTask;
                    },  
                    OnHalfOpened = (args) => 
                    {
                        Console.WriteLine("Circuit Breaker - On Half Opened");
                        return ValueTask.CompletedTask;
                    }
    })
    .AddTimeout(TimeSpan.FromSeconds(2));

builder
    .AddChaosFault(FAULT_INJECTION_RATE, () => new InvalidOperationException("Injected by chaos strategy!")) // Inject a chaos fault to executions
    .AddChaosLatency(LATENCY_INJECTION_RATE, TimeSpan.FromMilliseconds(500)) // Inject a chaos latency to executions
    .AddChaosOutcome(OUTCOME_INJECTION_RATE, () =>
                        new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)) // Inject a chaos outcome to executions
    .AddChaosBehavior(BEHAVIOR_INJECTION_RATE, cancellationToken =>
                            {
                                // introduce a Custom Fault 
                                return ValueTask.CompletedTask;
                            }); // Inject a chaos behavior to executions

ResiliencePipeline<HttpResponseMessage> pipeline = builder.Build();

var combinedPolicy = pipeline.AsAsyncPolicy();

// Execute the policy

//for (int i = 0; i < 200; i++)
while(true)
{

    try
    {
        await combinedPolicy.ExecuteAsync(DOAsync);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Operation failed: {ex.Message}: {ex.GetType().Name}");
    }
}

Console.WriteLine("Application completed");

static async Task<HttpResponseMessage> DOAsync()
{
    Console.WriteLine("DOAsync completed successfully");
    return await Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
}

