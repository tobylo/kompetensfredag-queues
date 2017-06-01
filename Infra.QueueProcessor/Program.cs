using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using DM.QueueService;
using DM.QueueService.Contracts;
using DM.QueueService.Services;
using Infra.QueueProcessor.MessageModels;

namespace Infra.QueueProcessor
{
    class Program
    {
        private static EventProxy _eventProxy;
        private static QueueMessageValidator _queueMessageValidator;

        // Required settings
        private static IQueueService _queueService;

        static void Main(string[] args)
        {
            InitEventProxyAndMessageValidator();

            /* TODO: Set up queue consumer configuration:
             * 1. Update the AWSCredentials with the information you received
             * 2. New up your AWSQueueService with the credentials and information given to you
             * 3. Set up your QueueConfiguration with the given information 
             * 4. Receive messages
             * 5. Process messages and publish your result to the outgoing queue
             * 7. Delete the successfully processed messages from the in-queue
             * 8. Profit
             */

            AWSCredentials credentials = null;
            QueueConfiguration queueConfiguration = null;

            _queueService = null;

            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                try
                {
                    throw new NotImplementedException($"You can do better!");
                    var successfullyProcessedMessages = ProcessMessages(null);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Nope, not like that. {e.Message}");
                }
            }
        }

        private static IEnumerable<Message<InMessage>> ProcessMessages(IEnumerable<Message<InMessage>> messages)
        {
            var successfullyProcessedMessages = new List<Message<InMessage>>();

            // TODO: Act!

            return successfullyProcessedMessages;
        }

        #region Free setup
        private static void InitEventProxyAndMessageValidator()
        {
            _eventProxy = new EventProxy();
            _eventProxy.RegisterHandler(
                LogLevel.Debug | LogLevel.Info | LogLevel.Warning | LogLevel.Error,
                (sender, evt) => Console.WriteLine($"{evt.Level}: {evt.Message}. {evt.Exception?.Message}"));
            _queueMessageValidator = new QueueMessageValidator(_eventProxy);
        }
        #endregion
    }
}