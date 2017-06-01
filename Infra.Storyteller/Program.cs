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
        private static Story story;

        static void Main(string[] args)
        {
            InitEventProxyAndMessageValidator();


            AWSCredentials credentials = new BasicAWSCredentials("", "");
            QueueConfiguration queueConfiguration = new QueueConfiguration(QueueType.AmazonSimpleQueueService, "");

            _queueService = new AwsQueueService(new AmazonSQSClient(credentials, RegionEndpoint.EUWest1),
                _queueMessageValidator, _eventProxy);

            story = new Story
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

            PostStartMessage();
            PostToSlack("Vi börjar om 15 minuter");
            Thread.Sleep(TimeSpan.FromMinutes(15));
            PostCountDownToSlack();
            var messagesToSend = story.Parts.Select(ToMessage).ToList();
            foreach (var message in messagesToSend)
            {
                PostToInputQueue(message);
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

        private static void PostToInputQueue(Message<InMessage> message)
        {
            _queueService.SendMessageAsync(new QueueConfiguration(QueueType.AmazonSimpleQueueService, ""), message)
                .Wait();
        }

        private static void PostCountDownToSlack()
        {
            for (int i = 10; i > 0; i--)
            {
                PostToSlack($"{i}!");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        private static void PostStartMessage()
        {
            var text =
                $"Hjälp mig att berätta en saga. Delta genom att hämta hem projektet från: https://github.com/tobylo/kompetensfredag-queues";
            PostToSlack(text);
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
                var part = story.Parts.SingleOrDefault(x => x.Id == message.CorrelationId);
                if (part != null)
                {
                    part.MissingWord = message.Data.Word;
                    part.SolvedBy = message.Data.TeamName;
                }
                else
                {
                    PostToSlack($"Nu glömde {message.Data.TeamName} att följa best practice för spårbarhet av meddelanden");
                }
                successfullyProcessedMessages.Add(message);
            }

            PostStoryToSlack();
            PostCurrentScoreToSlack();
            return successfullyProcessedMessages;
        }

        private static void PostCurrentScoreToSlack()
        {
            var scores =
                story.Parts.GroupBy(x => x.SolvedBy).Where(x => !string.IsNullOrEmpty(x.Key))
                    .Select(x => new {TeamName = x.Key, Score = x.Count()})
                    .OrderByDescending(x => x.Score)
                    .Take(3);

            var text = $"*Poäng:*\n{string.Join("\n", scores.Select((x,i) => $"*{i + 1}.* *{x.TeamName} ({x.Score})*"))}";
            PostToSlack(text);
        }

        private static void PostStoryToSlack()
        {
            PostToSlack(story.ToString());
        }

        private static void PostToSlack(string text)
        {
            
            var client = new SlackClient(SlackClient.DefaultWebHookUri);
            client.SendSlackMessage(new SlackMessage
            {
                Channel = "infra-test",
                Text = text,
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

            public override string ToString() => $"{_text} *{MissingWord}*";
            public Part(string text, WordClass missingWordClass)
            {
                MissingWordClass = missingWordClass;
                _text = text; 
                Id = Guid.NewGuid();
            }

            public string MissingWord { get; set; } = "[_____]";
            public WordClass MissingWordClass { get; set; }
            public Guid Id { get; }
            public string SolvedBy { get; set; }
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
