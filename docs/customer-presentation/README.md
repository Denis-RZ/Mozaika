# Пакет презентации для заказчика

## Что внутри
- `mozaika-presentation.html` - визуальная презентация.
- `demo-script-ru.md` - сценарий живого показа по шагам.
- `assets/` - реальные файлы, сгенерированные API (PNG/JPEG/SVG/PDF/CSV).

## Как обновить демо-материалы
1. Убедиться, что backend поднят на `http://127.0.0.1:8000`.
2. Выполнить:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-customer-presentation-assets.ps1
```

Скрипт автоматически:
- логинится в систему,
- генерирует тестовую мозаику,
- сохраняет экспортные артефакты в `docs/customer-presentation/assets`.

## Как показать презентацию
1. Открыть в браузере:
`docs/customer-presentation/mozaika-presentation.html`
2. Во время встречи параллельно использовать:
`docs/customer-presentation/demo-script-ru.md`

## Что важно для наглядности
- В презентации используются реальные экспортные файлы.
- Метрики (чипы, цвета, цена) подтягиваются из `assets/summary.json` автоматически.
- Если JSON не загрузился (например, строгие ограничения browser `file://`), остаются корректные fallback-значения.
