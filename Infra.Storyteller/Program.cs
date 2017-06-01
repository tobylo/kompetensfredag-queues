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
            QueueConfiguration outputQueueConfiguration = 
                new QueueConfiguration(QueueType.AmazonSimpleQueueService, "");

            _queueService = new AwsQueueService(new AmazonSQSClient(credentials, RegionEndpoint.EUWest1),
                _queueMessageValidator, _eventProxy);

            story = new Story
            {
                Parts = new List<Part>
                {
                    new Part("*En liten saga*\nDet var en gång en", WordClass.AdjektivSingular),
                    new Part("", WordClass.SubstantivSingularObestamd),
                    new Part("som bodde i", WordClass.Plats),
                    new Part(".\nVarje morgon brukade", WordClass.SubstantivSingularBestamd),
                    new Part("gå till en", WordClass.SubstantivSingularObestamd),
                    new Part(". Där brukade hen träffa sin vän, som kallades", WordClass.SubstantivSingularBestamd),
                    new Part(". Tillsammans brukade de gå ut och", WordClass.VerbGrundform),
                    new Part(". Det gillade de, eftersom de alltid blev så", WordClass.AdjektivPlural),
                    new Part("av det.\nEn dag var inte som alla andra. När de kom till", WordClass.SubstantivSingularBestamd),
                    new Part("var allting borta.", WordClass.SubstantivPluralBestamd),
                    new Part("blev förstås väldigt förvånade och bestämde sig för att hämta ett par", WordClass.SubstantivPluralObestamd),
                    new Part(". De visste att sådana skulle finnas i", WordClass.Plats),
                    new Part(".\nPå vägen dit mötte de en gammal gubbe. Han tittade häpet på dem och sa:\"", WordClass.Interjektion),
                    new Part("!\" De gamla vännerna tittade på varandra och", WordClass.VerbPreteritum),
                    new Part(". De visste inte riktigt vad de skull tro. Men efter att ha", WordClass.VerbSupinum),
                    new Part("en stund kom de fram till att det nog var bäst att", WordClass.VerbGrundform),
                    new Part("ordentligt. Det brukade alltid göra saker lite klarare. Så det gjorde de. Efter ungefär en timme tyckte de att def fick räcka. Det var dags att gå hem igen. De sa hejdå till gubben och bestämde sig för att träffas igen nästa vecka och fortsätta.\nNär", WordClass.SubstantivSingularBestamd),
                    new Part("kom hem den kvällen kändes det som att dagen hade varit", WordClass.AdjektivSingular),
                    new Part(". Det kan bli så när det händer någonting utöver det vanliga. \"Oj, vad jag är", WordClass.AdjektivSingular),
                    new Part("\" tänkte hen och", WordClass.VerbPreteritum)
                }
            };

            PostStartMessage();
            PostToSlack("Vi börjar om 25 minuter");
            Thread.Sleep(TimeSpan.FromMinutes(15));
            PostToSlack("10 minuter kvar");
            Thread.Sleep(TimeSpan.FromMinutes(5));
            PostToSlack("5 minuter kvar");
            Thread.Sleep(TimeSpan.FromMinutes(4));
            PostToSlack("1 minut till start");
            Thread.Sleep(TimeSpan.FromMinutes(1));
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
                            outputQueueConfiguration).Result;
                    if (!messages.Any()) continue;

                    var successfullyProcessedMessages = ProcessMessages(messages);

                    _queueService.DeleteMessagesAsync(outputQueueConfiguration, successfullyProcessedMessages).Wait();
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
            for (int i = 5; i > 0; i--)
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
