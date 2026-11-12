using Godot;
using System;

/// Компонент здоровья. Отвечает за хранение текущего запаса прочности
/// сущности, получение урона и уничтожение родительского объекта
/// при достижении нуля. При смерти испускает сигнал Died.
[GlobalClass]
public partial class HealthComponent : Node
{
    // Сигнал смерти. На него подписываются другие объекты
    // (например, Core вызывает конец игры, GameManager начисляет металл).
    [Signal]
    public delegate void DiedEventHandler();

    // Ссылка на родительский узел (башню или врага), которому принадлежит это здоровье
    private Node3D parentNode;

    // Максимальное значение здоровья.
    [Export]
    protected float MaxHealth = 100f;

    // Текущее значение здоровья
    protected float CurrentHealth = 100f;

    // Флаг смерти: защищает от повторного вызова Die() и сигнала
    private bool _isDead = false;

    // Вызывается при инициализации узла в дереве сцены.
    public override void _Ready()
    {
        // Получаем родительский узел
        parentNode = GetParent<Node3D>();
        // При старте игры здоровье всегда равно максимуму
        CurrentHealth = MaxHealth;
    }

    // Публичный метод для получения урона. Вызывается из DamagingComponent.
    public void TakeDamage(float damage)
    {
        // Уже мёртвые объекты урон не получают
        if (_isDead) return;

        // Уменьшаем текущее здоровье на полученный урон
        CurrentHealth -= damage;

        // Если здоровье упало до нуля или ниже — вызываем метод смерти
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    // Приватный метод, отвечающий за смерть сущности.
    private void Die()
    {
        _isDead = true;

        // Сначала сообщаем подписчикам, затем удаляем объект
        EmitSignal(SignalName.Died);

        /// QueueFree() безопасно удаляет узел из дерева сцены в конце текущего кадра.
        /// Поскольку parentNode — это корневой узел врага или башни,
        /// удалится вся сущность целиком.
        parentNode.QueueFree();
    }
}