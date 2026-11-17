# FinGuard AML Platform — практическая работа №6

## Что готово

- `frontend/`: веб-интерфейс (HTML + CSS + JS), готовый к ручной загрузке на Netlify.
- `backend-additions/`: новые файлы для **существующего** проекта `FinGuardAML`.
- `FinGuardAnalyticsApi/`: отдельный дополнительный учебный REST API (по желанию развернуть на Amvera), работающий как второй сервис.

ВНИМАНИЕ: этот дополнительный API разработан **самостоятельно** и не является API одногруппника. Функциональная интеграция может быть показана, но требование задания «API одногруппника» будет выполнено только после подключения реального API одногруппника или разрешения преподавателя заменить его своим вторым сервисом.

## 1. Дополняем backend FinGuard AML

Скопировать:
- `backend-additions/Controllers/PlatformController.cs` -> `FinGuardAML/Controllers/PlatformController.cs`
- `backend-additions/Services/PartnerAnalyticsService.cs` -> `FinGuardAML/Services/PartnerAnalyticsService.cs`

Открыть `FinGuardAML/Program.cs` и сразу после существующей регистрации `OpenSanctionsScreeningService` добавить:

```csharp
builder.Services.AddHttpClient<PartnerAnalyticsService>(client =>
    client.Timeout = TimeSpan.FromSeconds(7));
```

Ничего не менять в старых `RiskController`, `CurrencyController`, `RiskScoringService`, `TransactionRequest`.

Пересобрать проект в Visual Studio. Новые маршруты:
- `GET /api/v1/platform/status` — состояние подключения второго сервиса.
- `POST /api/v1/platform/analyze` — расчёт риска + попытка дополнительной аналитики; в отсутствии второго API состояние `PARTIAL`.

Перед публикацией на Amvera убедитесь, что старый `POST /api/v1/risk-score` продолжает работать. Для Amvera загрузите/отправьте изменённый исходный код и пересоберите Docker-контейнер. Само изменение GitHub не обязательно обновляет отдельное хранилище кода Amvera.

## 2. Дополнительный API (не одногруппника!)

В папке `FinGuardAnalyticsApi` уже есть `FinGuardAnalyticsApi.csproj`, `Program.cs`, `Dockerfile`, `amvera.yaml`. Это отдельное .NET 8 приложение. В нём:

- GET `/api/v1/analytics/health` => `{ "service": "FinGuard Analytics API", "status": "OK" }`
- POST `/api/v1/analytics/analyze` с JSON полями `clientId`, `amountRub`, `operationsLast30Days`, `totalAmountLast30Days`.

Пример запроса:

```json
{"clientId":"TEST-002","amountRub":824529,"operationsLast30Days":25,"totalAmountLast30Days":2500000}
```

Пример ожидаемого ответа:

```json
{"additionalScore":10,"signal":"UNUSUAL_VS_AVERAGE","explanation":"Сумма операции как минимум втрое превышает среднюю по истории клиента (+10).","typicalAmountRub":100000,"deviationRatio":8.25}
```

Развёртывание дополнительного сервиса в Amvera нужно делать отдельным приложением Docker, загрузив содержимое `FinGuardAnalyticsApi` в корень его репозитория. Это может потребовать отдельной оплаты. Для предварительной проверки можно запустить локально `dotnet run --urls http://localhost:5180` из этой папки.

Когда появится внешний адрес второго сервиса, на Amvera у **основного** FinGuard AML установить переменную среды:

`PartnerAnalytics__BaseUrl=https://АДРЕС-АНАЛИТИКИ.amvera.tech`

Перезапустить основной backend, проверить `GET /api/v1/platform/status`. В ответе должно появиться `partnerApi: AVAILABLE`. После `POST /api/v1/platform/analyze` ответ должен иметь `integrationStatus: COMBINED`.

Без переменной `PartnerAnalytics__BaseUrl` основной API продолжает штатно работать, новый endpoint возвращает `PARTIAL`. Не пытайтесь передавать URL второго сервиса из браузера: он задаётся только администратором через конфигурацию.

## 3. Frontend на Netlify

Папка `frontend/` полностью готова. Загрузить **папку целиком** через https://app.netlify.com/drop (после входа в Netlify). В корне папки должны находиться:

- `index.html`
- `styles.css`
- `app.js`
- `_redirects`

Файл `_redirects` настроен на проксирование `/api/*` в существующий backend:

```
/api/*  https://finguardaml-megalord9.msk1.amvera.tech/api/:splat  200
```

Это позволяет frontend отправлять GET/POST в тот же origin сайта Netlify, не изменяя CORS в backend. Редирект `200` — прокси (rewrite), не переадресация браузера. На бесплатном домене Netlify можно пользоваться интерфейсом без установки Node.js или npm.

До обновления backend frontend может использовать старый `POST /api/v1/risk-score` как временный fallback и покажет «Только основной API». После публикации новых маршрутов появится общий результат с дополнительной аналитикой, если она настроена.

## 4. Что проверить и сфотографировать

1. Главное окно FinGuard AML Platform на Netlify.
2. GET `/api/v1/platform/status` с `partnerApi: AVAILABLE` (либо честно `NOT_CONFIGURED` до развёртывания).
3. POST `/api/v1/platform/analyze` с обычной операцией (статус 200).
4. POST с крупной операцией — 70 баллов основного API + 10 баллов дополнительного API (если он настроен).
5. История транзакций и визуализация `LOW`/`HIGH`.
6. Некорректная сумма (`-5000`) — ответ HTTP 400 и сообщение об ошибке.
7. Отключённый второй сервис — `PARTIAL` без потери основного расчёта.
8. GitHub, Amvera, Netlify со ссылками.

Если второе приложение пока не развёрнуто, не заявляйте в отчёте, что оно работает: сначала тестируйте частичный режим.

## Безопасность и ограничения

- Обе платформы учебные, используйте только вымышленные данные.
- Ключ OpenSanctions в публичную облачную версию не добавлять.
- Существующий API без авторизации; при публикации избегайте реальных персональных данных.
- Не считайте итоговый балл юридическим заключением; правила скоринга демонстрационные.
- История хранится в памяти и исчезает при перезапуске.
