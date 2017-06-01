using System;

namespace Infra.QueueProcessor.MessageModels
{
    public enum WordClass
    {
        SubstantivPluralObestamd,
        SubstantivSingularObestamd,
        SubstantivSingularBestamd,
        VerbPreteritum,
        VerbSupinum,
        AdjektivSingular,
        AdjektivPlural,
        Plats,
        Interjektion,
        VerbGrundform,
        SubstantivPluralBestamd
    }

    public class InMessage
    {
        public WordClass RequestedWordClass { get; set; }
    }
}