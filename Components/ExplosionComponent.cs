using Godot;
using System.Collections.Generic;

/// Компонент урона по площади.
/// Наследуется от DamagingComponent. При попадании в цель наносит урон
/// не только ей, но и всем подходящим целям (из TargetGroups) в радиусе ExplosionRadius
/// от точки попадания. По желанию урон уменьшается к краю взрыва.
[GlobalClass]
public partial class ExplosionComponent : DamagingComponent
{
    // Радиус взрыва (в единицах сцены)
    [Export]
    public float ExplosionRadius = 4f;

    // Ослаблять ли урон с расстоянием от центра взрыва
    [Export]
    public bool UseFalloff = true;

    // Какая доля урона достаётся цели на самом краю радиуса.
    // Работает только при UseFalloff = true
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float EdgeDamagePercent = 0.5f;

    // Необязательная сцена визуального эффекта взрыва (например, вспышка или частицы).
    // Появляется в точке взрыва и должна сама удаляться
    [Export]
    public PackedScene ExplosionEffect;

    /// Переопределённый метод нанесения урона. Находит все цели в радиусе взрыва,
    /// наносит каждой урон (с учётом затухания), создаёт визуальный эффект
    /// и удаляет снаряд.
    public override void ApplyDamage(Node target)
    {
        if (HasHit)
        {
            return;
        }
        // Центр взрыва — позиция компонента в момент попадания
        Vector3 center = GlobalPosition;

        // Собираем все цели в радиусе (HashSet исключает дубли,
        // если цель состоит сразу в нескольких целевых группах)
        HashSet<Node3D> victims = CollectTargets(center, target);

        foreach (Node3D victim in victims)
        {
            if (!GodotObject.IsInstanceValid(victim)) continue;

            HealthComponent health = FindHealth(victim);
            if (health == null)
            {
                continue;
            }

            // Расстояние до центра ограничиваем радиусом: цель, в которую попала пуля,
            // могла оказаться чуть дальше из-за размеров модели
            float distance = Mathf.Min(center.DistanceTo(victim.GlobalPosition), ExplosionRadius);

            float multiplier = 1f;
            if (UseFalloff && ExplosionRadius > 0f)
                multiplier = Mathf.Lerp(1f, EdgeDamagePercent, distance / ExplosionRadius);

            health.TakeDamage(Damage * multiplier);
        }

        SpawnEffect(center);

        // Как в базовом классе: после срабатывания удаляем владельца (снаряд)
        if (DestroyAfterHit)
        {
            HasHit = true;
            GetParent().QueueFree();
        }
    }

    // Находит все подходящие цели в радиусе взрыва
    private HashSet<Node3D> CollectTargets(Vector3 center, Node hitTarget)
    {
        HashSet<Node3D> result = new HashSet<Node3D>();

        // Цель, в которую попали, входит в список всегда
        if (hitTarget is Node3D hit && IsValidTarget(hit))
            result.Add(hit);

        // Остальные берём из групп по расстоянию до центра взрыва
        foreach (string group in TargetGroups)
        {
            foreach (Node node in GetTree().GetNodesInGroup(group))
            {
                if (node is Node3D node3D
                    && GodotObject.IsInstanceValid(node3D)
                    && center.DistanceTo(node3D.GlobalPosition) <= ExplosionRadius)
                {
                    result.Add(node3D);
                }
            }
        }

        return result;
    }

    // Ищет HealthComponent среди дочерних узлов цели
    private HealthComponent FindHealth(Node node)
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

    // Создаёт визуальный эффект взрыва в точке центра
    private void SpawnEffect(Vector3 center)
    {
        if (ExplosionEffect == null)
        {
            GD.Print("[Explosion] ExplosionEffect не назначен, визуального эффекта не будет");
            return;
        }

        Node3D effect = ExplosionEffect.Instantiate<Node3D>();

        // Если это ExplosionFlash — подгоняем размер вспышки под радиус взрыва
        if (effect is ExplosionFlash flash)
            flash.Radius = ExplosionRadius;

        GetTree().CurrentScene.AddChild(effect);
        effect.GlobalPosition = center;
    }
}