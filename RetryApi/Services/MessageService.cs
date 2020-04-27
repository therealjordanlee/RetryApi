using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using RetryApi.Repositories;
using System;
using System.Threading.Tasks;

namespace RetryApi.Services
{
    public interface IMessageService
    {
        Task<string> GetHelloMessage();
        Task<string> GetGoodbyeMessage();
    }

    public class MessageService : IMessageService
    {
        private IMessageRepository _messageRepository;
        private AsyncRetryPolicy _retryPolicy;
        private AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

        public MessageService(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(2, retryAttempt => {
                    var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    Console.WriteLine($"Waiting {timeToWait.TotalSeconds} seconds");
                    return timeToWait;
                    }
                );

            _circuitBreakerPolicy = Policy.Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMinutes(1),
                (ex, t) =>
                {
                    Console.WriteLine("Circuit broken!");
                },
                () =>
                {
                    Console.WriteLine("Circuit Reset!");
                });
        }

        public async Task<string> GetHelloMessage()
        {
            return await _retryPolicy.ExecuteAsync<string>(async () => await _messageRepository.GetHelloMessage());
        }

        public async Task<string> GetGoodbyeMessage()
        {
            try
            {
                Console.WriteLine($"Circuit State: {_circuitBreakerPolicy.CircuitState}");
                return await _circuitBreakerPolicy.ExecuteAsync<string>(async () =>
                {
                    return await _messageRepository.GetGoodbyeMessage();
                });
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
