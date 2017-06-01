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
        VerbInfinitiv,
        AdjektivSingular,
        AdjektivPlural,
        Plats,
        Interjektion,
        VerbGrundform
    }

    public class InMessage
    {
        public WordClass RequestedWordClass { get; set; }
    }
}