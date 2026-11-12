using Godot;
using System;

/// Компонент движения. Отвечает за перемещение родительского узла (CharacterBody3D)
/// к заданной цели. Чаще всего используется для врагов.
[GlobalClass] // Позволяет добавлять этот компонент через меню "Добавить узел" в редакторе Godot
public partial class MoveComponent : Node
{
    // Ссылка на родительский узел, который мы будем двигать
    protected CharacterBody3D parentNode;

    // Текущая цель, к которой нужно двигаться
    protected Node3D Target;

    // Скорость передвижения
    [Export]
    protected float Speed = 50f;

    // Радиус остановки. Если расстояние до цели меньше этого значения, объект перестанет двигаться
    [Export]
    protected float StopRadius = 0.5f;


    // Вызывается при инициализации узла.
    public override void _Ready()
    {
        // Проверяем, является ли родительский узел CharacterBody3D.
        // Если да — сохраняем ссылку для дальнейшего использования.
        if (GetParent() is CharacterBody3D p)
            parentNode = p;
    }


    /// Публичный метод для установки текущей цели.
    /// Вызывается из VisibilityComponent, когда тот находит врага.
    public void SetTarget(Node3D target)
    {
        Target = target;
    }


    // Вызывается каждый физический кадр (синхронизировано с физикой движка).
    public override void _PhysicsProcess(double delta)
    {
        // Если родительское тело найдено, пытаемся двигаться
        if (parentNode != null)
            Move(parentNode);
    }


    // Основная логика передвижения.
    private void Move(CharacterBody3D p)
    {
        // Цель могла быть удалена (QueueFree) после последнего SetTarget.
        // Обращение к удалённому объекту вызывает ObjectDisposedException,
        // поэтому сбрасываем такую цель.
        if (Target != null && !GodotObject.IsInstanceValid(Target))
            Target = null;

        Vector3 diff;

        // Вычисляем вектор направления.
        // Если цели нет, движемся к центру карты (координаты 0,0,0).
        if (Target == null)
            diff = (Vector3.Zero - p.GlobalPosition);
        // Если цель есть, вычисляем вектор от текущей позиции до цели.
        else
            diff = Target.GlobalPosition - p.GlobalPosition;

        // Нормализуем вектор, чтобы получить чистое направление
        Vector3 direction = diff.Normalized();

        // Проверяем, находится ли цель дальше, чем радиус остановки
        if (diff.Length() > StopRadius)
        {
            // Создаем вектор скорости.
            // Ось Y принудительно равна 0, чтобы персонаж не летал вверх/вниз, а двигался только по плоскости.
            Vector3 velocity = new Vector3(
                direction.X * Speed,
                0,
                direction.Z * Speed
            );

            // Применяем скорость к телу и вызываем MoveAndSlide
            p.Velocity = velocity;
            p.MoveAndSlide();
        }
        // Если цель в пределах радиуса остановки, гасим скорость
        else
            p.Velocity = Vector3.Zero;
    }
}