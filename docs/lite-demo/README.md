# Lite JS Demo (No Backend)

Single-page JavaScript mosaic demo for GitHub Pages.

Path:

- `docs/lite-demo/index.html`

Features:

- image upload
- field/chip/gap setup
- palette input in HEX
- max color limit
- zoom/pan on source projection
- PNG export

Run locally:

```powershell
cd docs/lite-demo
python -m http.server 8088
```

Open:

- `http://127.0.0.1:8088`

GitHub Pages:

- Деплой идёт через workflow (Actions → Deploy Lite Demo to GitHub Pages).
- Ссылка на демо: `https://<org-or-user>.github.io/<repo>/` (редирект на `/lite-demo/`).

**Как показать страницу заказчику, не открывая репозиторий:**

1. В настройках репозитория: **Settings → General → Danger zone → Change repository visibility → Make private**.
2. Pages при этом остаётся доступен по той же публичной ссылке — заказчик открывает только собранный сайт, без доступа к коду и без возможности клонировать репозиторий.
