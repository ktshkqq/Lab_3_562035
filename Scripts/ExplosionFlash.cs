using Godot;

/// Визуальный эффект взрыва: полупрозрачная сфера, которая быстро растёт
/// до нужного радиуса, одновременно исчезая, и затем удаляется сама.
/// Меш и материал создаются из кода, поэтому сцене достаточно одного узла Node3D
/// с этим скриптом.
[GlobalClass]
public partial class ExplosionFlash : Node3D
{
    // Конечный радиус вспышки. ExplosionComponent подставляет сюда свой ExplosionRadius
    [Export]
    public float Radius = 4f;

    // Длительность эффекта, секунд
    [Export]
    public float Duration = 0.4f;

    // Цвет вспышки (по заданию: зелёные для башен игрока, красные для врагов)
    [Export]
    public Color FlashColor = new Color(1f, 0.6f, 0.1f);

    public override void _Ready()
    {
        // Полупрозрачный материал без освещения (светится одним цветом)
        StandardMaterial3D material = new StandardMaterial3D
        {
            AlbedoColor = new Color(FlashColor, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        // Сфера единичного радиуса: размер задаём через Scale
        MeshInstance3D sphere = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f },
            MaterialOverride = material,
            Scale = Vector3.One * 0.1f // стартуем с маленькой сферы
        };
        AddChild(sphere);

        // Анимация: сфера растёт и одновременно становится прозрачной
        Tween tween = CreateTween();
        tween.SetParallel(true);

        tween.TweenProperty(sphere, "scale", Vector3.One * Radius, Duration)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(material, "albedo_color:a", 0f, Duration);

        // После окончания анимации удаляем эффект
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
