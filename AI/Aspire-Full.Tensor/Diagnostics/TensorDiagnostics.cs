using System.Diagnostics;

namespace Aspire_Full.Tensor;

public static class TensorDiagnostics
{
    public const string ActivitySourceName = "Aspire-Full.Tensor";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
