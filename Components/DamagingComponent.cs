using Godot;

/// Компонент нанесения урона. Наследуется от Area3D.
/// Вешается как дочерний узел на пули, врагов (Crawler), взрывы и т.п.
/// Режимы работы:
///  - DamageInterval = 0: урон один раз при входе цели в зону (пули);
///  - DamageInterval > 0: урон всем целям в зоне каждые N секунд (Crawler).
[GlobalClass]
public partial class DamagingComponent : Area3D
{
    // Количество наносимого урона
    [Export]
    public float Damage = 10f;

    // Группы, с которыми компонент взаимодействует (кого можно ранить)
    [Export]
    public string[] TargetGroups = { "Enemy", "Tower", "Core" };

    // Если true — после попадания удаляется владелец компонента (например, пуля)
    [Export]
    public bool DestroyAfterHit = true;

    // Если > 0 — урон наносится постоянно, раз в N секунд, пока цель в зоне
    [Export]
    public float DamageInterval = 0f;

    // Защита от повторного срабатывания (BodyEntered + AreaEntered в одном кадре)
    protected bool HasHit = false;

    // Таймер для режима постоянного урона
    private float _timer = 0f;

    // Вызывается при добавлении узла в сцену.
    // Подписываемся на сигналы входа тел и зон в область урона.
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;
    }

    // Обработчик столкновения с физическим телом
    private void OnBodyEntered(Node3D body)
    {
        // В режиме постоянного урона всё делает _PhysicsProcess
        if (DamageInterval > 0f) return;

        TryDamage(body);
    }

    // Обработчик пересечения с другой зоной Area3D
    private void OnAreaEntered(Area3D area)
    {
        if (DamageInterval > 0f) return;

        TryDamage(area);
    }

    // Режим постоянного урона: раз в DamageInterval секунд бьём все цели в зоне
    public override void _PhysicsProcess(double delta)
    {
        if (DamageInterval <= 0f) return;

        _timer -= (float)delta;
        if (_timer > 0f) return;

        bool damagedAnyone = false;

        foreach (Node3D body in GetOverlappingBodies())
        {
            if (!GodotObject.IsInstanceValid(body)) continue;

            if (IsValidTarget(body))
            {
                ApplyDamage(body);
                damagedAnyone = true;

                if (HasHit) break; // владелец уже удалён
            }
        }

        // Перезаряжаем таймер, только если кого-то ударили
        if (damagedAnyone)
            _timer = DamageInterval;
    }

    // Проверяет, входит ли цель хотя бы в одну из TargetGroups
    protected bool IsValidTarget(Node target)
    {
        foreach (string group in TargetGroups)
        {
            if (target.IsInGroup(group))
                return true;
        }
        return false;
    }

    /// Проверяет, подходит ли цель по группам. Если да — вызывает ApplyDamage.
    protected virtual void TryDamage(Node target)
    {
        if (HasHit) return;

        if (IsValidTarget(target))
            ApplyDamage(target);
    }

    /// Наносит урон цели через её HealthComponent.
    /// Если DestroyAfterHit включён, удаляет владельца компонента (GetParent()),
    /// а не сам компонент — иначе пуля продолжит лететь без урона.
    public virtual void ApplyDamage(Node target)
    {
        if (HasHit) return;

        HealthComponent health = FindHealthComponent(target);

        if (health == null) return;

        health.TakeDamage(Damage);

        if (DestroyAfterHit)
        {
            HasHit = true;
            GetParent().QueueFree();
        }
    }

    // Ищет HealthComponent у самой цели или среди её прямых дочерних узлов
    private HealthComponent FindHealthComponent(Node node)
    {
        if (node is HealthComponent health)
            return health;

        foreach (Node child in node.GetChildren())
        {
            if (child is HealthComponent hc)
                return hc;
        }

        return null;
    }
}