namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §31 - AutoCAD.DatabaseServices.Database.PlotStyleMode(bool)를 그대로
/// 옮긴 도메인 열거값. AutoCAD Plugin이 실제 값을 읽어 IPC로 넘길 때 이 타입으로 변환한다 - IPC DTO에
/// bool 하나만 두면 어느 쪽이 CTB/STB인지 호출부마다 다시 외워야 한다.</summary>
public enum CadPlotStyleMode
{
    /// <summary>Color-Dependent Plot Style Tables (CTB) - Database.PlotStyleMode == false.</summary>
    ColorDependent,

    /// <summary>Named Plot Style Tables (STB) - Database.PlotStyleMode == true.</summary>
    Named
}
