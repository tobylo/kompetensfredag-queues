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
using Infra.Storyteller.Models;

namespace Infra.Storyteller
{
    class Program
    {
        private static EventProxy _eventProxy;
        private static QueueMessageValidator _queueMessageValidator;

        // Required settings
        private static IQueueService _queueService;
        private static Story sentences;

        static void Main(string[] args)
        {
            InitEventProxyAndMessageValidator();


            AWSCredentials credentials = new BasicAWSCredentials("", "");
            QueueConfiguration queueConfiguration = new QueueConfiguration(QueueType.AmazonSimpleQueueService, "");

            _queueService = new AwsQueueService(new AmazonSQSClient(credentials, RegionEndpoint.EUWest1),
                _queueMessageValidator, _eventProxy);

            sentences = new Story
            {
                Parts = new List<Part>
                {
                    new Part("Jag var ute och", WordClass.Verb),
                    new Part("en", WordClass.Adjective),
                    new Part("dag. Plötsligt kom en", WordClass.Noun),
                    new Part("från ingenstans och", WordClass.Verb),
                    new Part("", WordClass.Preposition),
                    new Part("mig.\nJag blev mycket", WordClass.Adjective),
                    new Part("eftersom det var en", WordClass.Adjective),
                    new Part("", WordClass.Noun),
                    new Part("!\nJag bestämde mig för att", WordClass.Verb),
                    new Part("innan", WordClass.Noun),
                    new Part("hunnit springa iväg.\nFörsiktigt", WordClass.Verb),
                    new Part("jag", WordClass.Noun),
                    new Part(", och gick sedan till", WordClass.Noun),
                    new Part("för att", WordClass.Verb),
                    new Part("", WordClass.Noun)
                }
            };

            var messagesToSend = sentences.Parts.Select(ToMessage).ToList();
            foreach (var message in messagesToSend)
            {
                _queueService.SendMessageAsync(new QueueConfiguration(QueueType.AmazonSimpleQueueService, ""), message)
                    .Wait();
            }

            PostStoryToSlack();

            while (true)
            {
                Thread.Sleep(100);
                try
                {
                    var messages =
                        _queueService.ReceiveMessagesAsync<OutMessage>(
                            queueConfiguration).Result;
                    if (!messages.Any()) continue;

                    var successfullyProcessedMessages = ProcessMessages(messages);

                    _queueService.DeleteMessagesAsync(queueConfiguration, successfullyProcessedMessages).Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Nope, not like that. {e.Message}");
                }
            }
        }

        private static Message<InMessage> ToMessage(Part part)
        {
            return Message.Create(new InMessage {RequestedWordClass = part.MissingWordClass}, part.Id);
        }


        private static IEnumerable<Message<OutMessage>> ProcessMessages(
            IEnumerable<Message<OutMessage>> messages)
        {
            var successfullyProcessedMessages = new List<Message<OutMessage>>();

            foreach (var message in messages)
            {
                sentences.Parts.Single(x => x.Id == message.CorrelationId).MissingWord = message.Data.Word;
                successfullyProcessedMessages.Add(message);
            }

            PostStoryToSlack();
            return successfullyProcessedMessages;
        }

        private static void PostStoryToSlack()
        {
            var client = new SlackClient(SlackClient.DefaultWebHookUri);
            client.SendSlackMessage(new SlackMessage
            {
                Channel = "infra-test",
                Text = sentences.ToString(),
                UserName = "Storyteller"
            });
        }

        public class Story
        {
            public override string ToString() => string.Join(" ", Parts);
            public List<Part> Parts { get; set; }
        }

        public class Part
        {
            private readonly string _text;

            public Part(string text, WordClass missingWordClass)
            {
                MissingWordClass = missingWordClass;
                _text = $"{text} {MissingWord}";
            }

            public string Text => _text;
            public string MissingWord { get; set; } = "______";
            public Infra.QueueProcessor.MessageModels.WordClass MissingWordClass { get; set; }
            public Guid Id => Guid.NewGuid();
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
