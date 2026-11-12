using Godot;
using System;

[GlobalClass]
/// Класс пули. Наследуется от Area3D для обнаружения триггеров
/// без физического отталкивания объектов.
public partial class Bullet: Area3D
{
    // Базовая скорость полета пули
    private float Speed = 100f;

    // Устанавливает скорость пули (вызывается из ShootComponent при спавне)
    public void SetSpeed(float s)
    {
        Speed = s;
    }

    // Вызывается каждый физический кадр. Отвечает за передвижение пули
    public override void _PhysicsProcess(double delta)
    {
        // Движение вперед по локальной оси -Z
        // Умножение на delta обеспечивает плавное движение, независимое от FPS
        Position += -Transform.Basis.Z * Speed * (float)delta;
    }
}
