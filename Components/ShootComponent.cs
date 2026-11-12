using Godot;
using System;

/// Компонент стрельбы. Отвечает за наведение на цель, контроль скорострельности
/// и создание (спавн) снарядов (пуль).
[GlobalClass] // Позволяет добавлять компонент через меню создания узлов в редакторе
public partial class ShootComponent : Node3D
{
    // Ссылка на сцену пули, которую нужно создавать
    [Export] private PackedScene BulletScene;

    // Урон, который будет передан пуле
    [Export] private float BulletDamage = 10f;

    // Скорость пули
    [Export] private float BulletSpeed = 50f;

    // Скорострельность (выстрелов в секунду)
    [Export] private float FireRate = 2f;

    // Текущая цель
    private Node3D Target;

    // Таймер для контроля интервала между выстрелами
    private Timer shootTimer = new Timer();


    // Инициализация компонента при добавлении в сцену.
    public override void _Ready()
    {
        /// Защита от деления на ноль, если в инспекторе случайно указали 0
        /// или отрицательное значение
        if (FireRate <= 0) FireRate = 1f;

        // Время ожидания таймера = 1 секунда / выстрелов в секунду
        shootTimer.WaitTime = 1.0f / FireRate;

        // Отключаем режим "одного выстрела", чтобы таймер срабатывал циклично
        shootTimer.OneShot = false;

        // Добавляем таймер как дочерний узел к этому компоненту
        AddChild(shootTimer);

        // Подписываемся на сигнал таймера: как только время вышло, вызывается метод TryShoot
        shootTimer.Timeout += () => TryShoot();
    }


    // Попытка выстрела. Вызывается таймером.
    private void TryShoot()
    {
        // Если сцена пули назначена, создаем пулю
        if (BulletScene != null)
            SpawnBullet();
        else
            GD.Print("[!] BulletScene не назначена в Инспекторе!"); // Предупреждение, если забыли назначить сцену
    }


    // Создание и настройка пули.
    private void SpawnBullet()
    {
        if (BulletScene == null) return; // Дополнительная проверка на всякий случай

        // Инстанцируем экземпляр пули из сцены
        Bullet bullet = BulletScene.Instantiate<Bullet>();

        // Добавляем пулю в текущую активную сцену (чтобы она не была дочерней для башни и могла лететь независимо)
        GetTree().CurrentScene.AddChild(bullet);

        /// Вычисляем смещение (offset) для точки спавна пули.
        /// -GlobalTransform.Basis.Z — это вектор "вперед".
        /// Умножаем на 1.5f, чтобы пуля появлялась немного перед дулом, а не внутри башни.
        Vector3 offset = -GlobalTransform.Basis.Z * 1.5f;

        // Устанавливаем глобальную позицию и поворот пули
        bullet.GlobalPosition = GlobalPosition + offset;
        bullet.GlobalRotation = GlobalRotation;

        // Передаем настройки из инспектора башни прямо в экземпляр пули
        bullet.SetSpeed(BulletSpeed);

        // Ищем компонент урона по ТИПУ среди дочерних узлов пули.
        // Так подходит любой наследник DamagingComponent (DoTComponent и др.),
        // а имя узла в сцене пули не важно.
        DamagingComponent damageComponent = null;
        foreach (Node child in bullet.GetChildren())
        {
            if (child is DamagingComponent dc)
            {
                damageComponent = dc;
                break;
            }
        }

        if (damageComponent != null)
            damageComponent.Damage = BulletDamage;
        else
            GD.Print("[!] В сцене пули нет DamagingComponent: ", bullet.Name);
    }

    // Включение стрельбы (запуск таймера).
    private void TurnOnCannon()
    {
        // Если таймер остановлен — запускаем его
        if (shootTimer.IsStopped()) shootTimer.Start();
    }

    // Выключение стрельбы (остановка таймера).
    private void TurnOffCannon()
    {
        // Если таймер запущен — останавливаем его
        if (!shootTimer.IsStopped()) shootTimer.Stop();
    }

    // Публичный метод для установки цели. Вызывается из VisibilityComponent.
    public void SetTarget(Node3D target)
    {
        Target = target;
    }

    // Вызывается каждый кадр.
    public override void _Process(double delta)
    {
        // Если цель существует и она еще не уничтожена (IsInstanceValid)
        if (Target != null && GodotObject.IsInstanceValid(Target))
        {
            // Поворачиваем узел лицом к цели
            Vector3 direction = Target.GlobalPosition - GlobalPosition;
            direction.Y = 0;

            Rotation = new Vector3(
                Rotation.X,
                Mathf.Atan2(-direction.X, -direction.Z),
                Rotation.Z
            );

            // Включаем стрельбу
            TurnOnCannon();
        }
        else
        {
            // Если цели нет или она уничтожена
            Target = null;

            // Выключаем стрельбу, чтобы не тратить пули впустую
            TurnOffCannon();

            // Медленно вращаем башню вокруг своей оси (Y)
            // delta * 0.5f — скорость вращения
            Rotation = new Vector3(Rotation.X, Rotation.Y + (float)delta * 0.5f, Rotation.Z);
        }
    }
}