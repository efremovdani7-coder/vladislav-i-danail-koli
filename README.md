# 🚗 АвтоНадеждност.бг — Car Reliability Platform

Уеб платформа за споделяне и преглед на данни за надеждността на автомобили, написана на **C# / ASP.NET Core 8.0**.

## ✅ Реализирани функции (User Stories)

| US | Описание | Статус |
|----|----------|--------|
| US01 | Въвеждане на нови данни за автомобили | ✅ |
| US02 | Справки по марка / година / модел | ✅ |
| US05 | Регистрация с nicknames | ✅ |
| US06 | Админ панел — преглед, редакция, изтриване | ✅ |
| US15 | Responsive дизайн (телефон, таблет, компютър) | ✅ |

### Допълнения спрямо програмните изисквания
- **US01** — поле „Годишни разходи за поддръжка (€)“ + валидация в браузъра (jQuery Validation)
- **US02** — каскадно меню Марка → Модел и обновяване на резултатите без презареждане на страницата
- **US05** — уникален никнейм, 3–20 символа (букви, цифри и `_`)
- **US06** — бутон „Блокирай / Отблокирай потребител“ в админ панела

> Съществуваща `app.db` се обновява автоматично при стартиране (добавя се новата колона и уникалният индекс), не е нужно да я триете.

## 🛠 Технологии

- **ASP.NET Core 8.0** MVC
- **Entity Framework Core** с SQLite
- **ASP.NET Core Identity** (автентикация + роли)
- **Bootstrap 5** + Bootstrap Icons (responsive)
- **SQLite** (лека база данни, без сървър)

## 🚀 Стартиране

### 1. Инсталирай .NET 8 SDK
Свали от: https://dotnet.microsoft.com/download/dotnet/8.0

### 2. Стартирай проекта
```bash
cd car-reliability-bg
dotnet restore
dotnet run
```

Сайтът ще стартира на: **https://localhost:5001** (или http://localhost:5000)

### 3. Администраторски акаунт (създава се автоматично)
- **Имейл:** admin@cars.bg
- **Парола:** admin123

## 📁 Структура на проекта

```
CarReliability/
├── Controllers/         # MVC контролери
│   ├── HomeController.cs
│   ├── CarsController.cs
│   ├── AccountController.cs
│   └── AdminController.cs
├── Models/              # Модели на данните
│   ├── ApplicationUser.cs
│   ├── CarBrand.cs
│   └── CarEntry.cs
├── ViewModels/          # View модели
├── Views/               # Razor views
│   ├── Shared/_Layout.cshtml
│   ├── Home/
│   ├── Cars/
│   ├── Account/
│   └── Admin/
├── Data/                # EF Core DbContext
├── wwwroot/             # Статични файлове (CSS, JS)
├── Program.cs           # Entry point + конфигурация
└── appsettings.json     # Настройки
```

## 🔧 Как да добавяш нови функции

### Добавяне на нов модел
1. Създай клас в `Models/`
2. Добави `DbSet<>` в `Data/ApplicationDbContext.cs`
3. Пусни `dotnet ef migrations add MigrationName`
4. Пусни `dotnet ef database update`

### Добавяне на нов контролер
1. Създай клас в `Controllers/`
2. Добави views в `Views/{ControllerName}/`

### Добавяне на нова страница
1. Добави action метод в съответния контролер
2. Създай .cshtml файл в `Views/`

## 📱 Responsive дизайн

Сайтът използва Bootstrap 5 grid система и е оптимизиран за:
- 📱 Телефони (< 576px)
- 📱 Таблети (576px - 992px)
- 💻 Десктоп (> 992px)
