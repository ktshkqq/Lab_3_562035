using Godot;

/// Компонент урона со временем.
/// Наследуется от DamagingComponent. При попадании наносит начальный урон (Damage),
/// затем повторяет TickDamage каждые TickInterval секунд, всего TickCount раз.
/// Эффект "навешивается" на цель, поэтому продолжает действовать,
/// даже если пуля уже исчезла.
[GlobalClass]
public partial class DoTComponent : DamagingComponent
{
    // Урон за один "тик"
    [Export]
    public float TickDamage = 5f;

    // Интервал между тиками, секунд
    [Export]
    public float TickInterval = 1f;

    // Количество тиков
    [Export]
    public int TickCount = 4;

    // Наносить ли обычный урон (Damage) в момент попадания
    [Export]
    public bool DealInitialHit = true;

    // Имя узла-таймера на цели
    private const string EffectName = "DoTEffect";

    // Имя метаданных таймера: сколько тиков осталось
    private const string RemainingMeta = "remaining";

    /// Переопределённый метод нанесения урона. Наносит начальный удар (если включён
    /// DealInitialHit), запускает на цели эффект урона со временем и удаляет снаряд.
    public override void ApplyDamage(Node target)
    {
        if (HasHit) return;

        // Ищем HealthComponent по типу среди дочерних узлов цели (как в базовом классе)
        HealthComponent health = null;
        foreach (Node child in target.GetChildren())
        {
            if (child is HealthComponent hc)
            {
                health = hc;
                break;
            }
        }

        if (health == null) return;

        // Начальный удар
        if (DealInitialHit)
        {
            health.TakeDamage(Damage);
        }

        // Запускаем эффект, только если цель ещё жива
        if (!health.GetParent().IsQueuedForDeletion())
        {
            StartDoT(health, target.Name);
        }

        // Как в базовом классе: после срабатывания удаляем владельца (пулю)
        if (DestroyAfterHit)
        {
            HasHit = true;
            GetParent().QueueFree();
        }
    }

    // Запускает эффект на цели или продлевает уже идущий
    private void StartDoT(HealthComponent health, string targetName)
    {
        // Если эффект уже идёт — НЕ пересоздаём таймер (иначе при частых попаданиях
        // он никогда не дойдёт до тика), а только сбрасываем счётчик тиков
        if (health.GetNodeOrNull(EffectName) is Timer existing)
        {
            existing.SetMeta(RemainingMeta, TickCount);
            return;
        }

        // Копируем параметры в локальные переменные: пуля может исчезнуть,
        // а таймер должен продолжать работать без обращения к this
        float tickDamage = TickDamage;

        Timer timer = new Timer();
        timer.Name = EffectName;
        timer.WaitTime = TickInterval;
        timer.OneShot = false;
        timer.SetMeta(RemainingMeta, TickCount);
        health.AddChild(timer); // таймер удалится вместе с целью

        timer.Timeout += () =>
        {
            // Цель могла быть уничтожена между тиками
            if (!GodotObject.IsInstanceValid(health))
            {
                timer.QueueFree();
                return;
            }

            int remaining = timer.GetMeta(RemainingMeta).AsInt32() - 1;
            timer.SetMeta(RemainingMeta, remaining);

            health.TakeDamage(tickDamage);

            if (remaining <= 0)
            {
                timer.QueueFree();
            }
        };

        timer.Start();
    }
}