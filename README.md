# DDoS Defense System

A distributed, high-performance L4/L7 DDoS protection system built with .NET 10. 
It features real-time traffic monitoring, centralized policy management, and automated kernel-level network blocking (`iptables`) for Linux servers.

## Особенности (Features)

*   **L4 & L7 Traffic Analysis:** Асинхронный парсинг логов Nginx (HTTP-флуд) и мониторинг активных TCP/UDP соединений транспортного уровня.
*   **Instant Mitigation:** Автоматическое применение жесткой изоляции атакующих узлов на нулевом кольце фаервола ОС Linux (`iptables -I INPUT 1 -j DROP`).
*   **Distributed Architecture:** Независимые службы-агенты, управляемые из единого централизованного узла (Coordinator).
*   **Live Monitoring:** Десктопная панель управления (WPF) с трансляцией потока трафика и атак в реальном времени через веб-сокеты (SignalR).
*   **Access Control:** Глобальная синхронизация черных (Blacklist) и белых (Whitelist) списков каждые 10 секунд с защитой от самоблокировки.
*   **Audit & History:** Полное журналирование инцидентов ИБ и действий администраторов в реляционной базе данных.

---

## Архитектура комплекса

Проект разделен на микросервисы и состоит из следующих компонентов:

1.  **DdosDefendSystem.Agent** (Ubuntu Linux Daemon): Фоновая служба, работающая с правами суперпользователя (`root`). Выполняет чтение логов веб-сервера (`tail -F`), анализ сокетов через утилиту `ss` и прямое взаимодействие с ядром ОС для управления правилами фаервола.
2.  **DdosDefendSystem.Coordinator** (ASP.NET Core API): Центральный узел принятия решений. Хранит политики безопасности, агрегирует метрики от агентов и использует алгоритм скользящего окна для детектирования аномалий.
3.  **DdosDefendSystem.AdminPanel** (WPF Client): Графическое рабочее место администратора информационной безопасности, построенное со строгим соблюдением паттерна MVVM.
4.  **DdosDefendSystem.Shared**: Общая библиотека моделей данных (DTO) для сериализации и обмена между сервисами.

---

## Технологический стек (Tech Stack)

*   **Core:** C#, .NET 10
*   **Backend:** ASP.NET Core Minimal API, SignalR (WebSockets)
*   **Database:** PostgreSQL, Entity Framework Core (Code-First)
*   **Client / UI:** WPF (Windows Presentation Foundation), Material Design
*   **Infrastructure:** Ubuntu Server, Nginx, iptables, bash-scripting

---

