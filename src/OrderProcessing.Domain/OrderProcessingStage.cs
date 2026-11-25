namespace OrderProcessing.Domain;

/// <summary>Pre-commit fault-injection points for a first submission with a usable identifier.</summary>
public enum OrderProcessingStage
{
    BeforeValidation,
    AfterDecision,
    AfterExposurePreparation,
    BeforePublication
}
