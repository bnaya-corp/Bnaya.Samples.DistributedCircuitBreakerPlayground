// Resources:
// - Circuit breaker resilience strategy: https://www.pollydocs.org/strategies/circuit-breaker.html
// - When you use the Polly circuit-breaker, make sure you share your Policy instances!:
//   https://andrewlock.net/when-you-use-the-polly-circuit-breaker-make-sure-you-share-your-policy-instances-2/

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

// 2% of all requests will be injected with chaos fault.
const double FAULT_INJECTION_RATE = 0.30;
// inject a latency.
const double LATENCY_INJECTION_RATE = 0.50;
// inject with outcome
const double OUTCOME_INJECTION_RATE = 0.10;
// injected a custom behavior.
const double BEHAVIOR_INJECTION_RATE = 0.20;


var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
// First, configure regular resilience strategies
builder
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage> 
            {
                MaxRetryAttempts = 2,                
                Delay = TimeSpan.FromMilliseconds(10), 
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<Exception>(ex => ex is not BrokenCircuitException),
                OnRetry = (args) => 
                {
                    Console.Write(" 🔄 ");
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
                        Console.Write(" ✋ ");
                        Console.WriteLine();
                        return ValueTask.CompletedTask;
                    },
                    Name = "PlaygroundCircuitBreaker",                    
                    OnClosed = (args) => 
                    {
                        Console.Write(" 🔗 ");
                        return ValueTask.CompletedTask;
                    },  
                    OnHalfOpened = (args) => 
                    {
                        Console.WriteLine(" 🧪 ");
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
    catch (BrokenCircuitException ex)
    {
        Console.Write("✗");
    }
    catch (InvalidOperationException ex)
    {
        Console.Write(" ~😣~ ");
    }
}


static async Task<HttpResponseMessage> DOAsync()
{
    Console.Write("✓.");
    return await Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
}

