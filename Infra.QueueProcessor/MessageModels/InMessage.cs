using System;

namespace Infra.QueueProcessor.MessageModels
{
    public enum WordClass
    {
        Noun,
        Verb,
        Adjective,
        Preposition
    }

    public class InMessage
    {
        public WordClass RequestedWordClass { get; set; }
    }
}