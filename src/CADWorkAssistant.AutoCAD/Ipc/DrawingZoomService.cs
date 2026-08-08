using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CADWorkAssistant.Core.Drawing;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// ZoomExtents/ZoomToBounds가 공유하는 실제 View 조작 로직. `_ZOOM _E` 같은 명령 문자열을 보내는
/// 대신 Managed API로 View를 직접 계산해서 설정한다 (§22 - locale 의존성/명령 상태 의존성을 피한다,
/// 리플렉션으로 실존을 확인한 <see cref="ViewTableRecord"/>/<see cref="Editor.SetCurrentView"/> 기반).
///
/// WCS 좌표를 View의 DCS(화면 좌표계)로 옮기는 변환은 임의의 3D 뷰(ViewDirection/Target/ViewTwist가
/// 전부 0/원점이 아닌 경우)에서도 맞도록 계산한다 - 이 앱의 실제 사용례는 대부분 정면 2D 평면도지만,
/// 축 정렬된 뷰만 가정하면 나중에 등각뷰 등에서 조용히 틀린 곳으로 Zoom하는 버그가 된다.
/// </summary>
internal static class DrawingZoomService
{
    /// <summary>Bounds와 화면을 딱 맞붙이지 않는다 - 10% 여유 (§24).</summary>
    private const double MarginFactor = 1.10;

    public static void ZoomToBounds(Document document, CadBoundsDto bounds)
    {
        var editor = document.Editor;
        var view = editor.GetCurrentView();

        var worldToDcs = BuildWorldToDcsMatrix(view);

        var corners = new[]
        {
            new Point3d(bounds.MinX, bounds.MinY, bounds.MinZ),
            new Point3d(bounds.MaxX, bounds.MinY, bounds.MinZ),
            new Point3d(bounds.MinX, bounds.MaxY, bounds.MinZ),
            new Point3d(bounds.MaxX, bounds.MaxY, bounds.MinZ),
            new Point3d(bounds.MinX, bounds.MinY, bounds.MaxZ),
            new Point3d(bounds.MaxX, bounds.MinY, bounds.MaxZ),
            new Point3d(bounds.MinX, bounds.MaxY, bounds.MaxZ),
            new Point3d(bounds.MaxX, bounds.MaxY, bounds.MaxZ),
        };

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

        foreach (var corner in corners)
        {
            var dcs = corner.TransformBy(worldToDcs);
            if (dcs.X < minX) minX = dcs.X;
            if (dcs.Y < minY) minY = dcs.Y;
            if (dcs.X > maxX) maxX = dcs.X;
            if (dcs.Y > maxY) maxY = dcs.Y;
        }

        var width = (maxX - minX) * MarginFactor;
        var height = (maxY - minY) * MarginFactor;

        // 점 하나(가로/세로 0)만 선택된 경우 Zoom이 완전히 찌그러지지 않도록 최소 크기를 준다.
        if (width <= 0) width = 1.0;
        if (height <= 0) height = 1.0;

        view.Width = width;
        view.Height = height;
        view.CenterPoint = new Point2d((minX + maxX) / 2.0, (minY + maxY) / 2.0);

        editor.SetCurrentView(view);
    }

    public static void ZoomToExtents(Document document, CadBoundsDto extents) => ZoomToBounds(document, extents);

    /// <summary>표준 AutoCAD .NET View DCS 변환 - PlaneToWorld(ViewDirection) 위에 Target 이동과
    /// ViewTwist 회전을 얹은 뒤 역행렬을 취한다. ViewDirection/ViewTwist/Target이 전부 기본값(정면
    /// 위에서 본 2D 평면도)이 아닌 경우에도 정확하게 동작한다.</summary>
    private static Matrix3d BuildWorldToDcsMatrix(ViewTableRecord view)
    {
        var matrix = Matrix3d.PlaneToWorld(view.ViewDirection);
        matrix = Matrix3d.Displacement(view.Target - Point3d.Origin) * matrix;
        matrix = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * matrix;
        return matrix.Inverse();
    }
}
