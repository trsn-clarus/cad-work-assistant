using System.Collections.Generic;

namespace CADWorkAssistant.Core.Drawing;

public sealed class GetLayersResponse
{
    public GetLayersResponse(IReadOnlyList<CadLayerDto> layers)
    {
        Layers = layers;
    }

    public IReadOnlyList<CadLayerDto> Layers { get; }
}
