import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import pptxgen from "pptxgenjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, "..");
const outPath = path.join(rootDir, "presentation", "Mozaika-client-presentation.pptx");

const SHAPE = {
  rect: "rect",
  roundRect: "roundRect",
  ellipse: "ellipse",
  line: "line",
};

const C = {
  bg: "F4F8FC",
  bgDark: "0F172A",
  panel: "FFFFFF",
  border: "D7E2EE",
  title: "12233D",
  text: "243A57",
  muted: "5A6B82",
  brand: "0D9488",
  brandDark: "0C4A52",
  blue: "1E63D8",
  green: "198754",
  amber: "B7791F",
  white: "FFFFFF",
};

const assets = {
  login: path.join(rootDir, "presentation", "assets", "ui", "01-login.png"),
  dashboard: path.join(rootDir, "presentation", "assets", "ui", "02-dashboard.png"),
  studio: path.join(rootDir, "presentation", "assets", "ui", "03-studio-source.png"),
  generated: path.join(rootDir, "presentation", "assets", "ui", "04-generated-preview.png"),
  exportBlock: path.join(rootDir, "presentation", "assets", "ui", "05-export-block.png"),
  projects: path.join(rootDir, "presentation", "assets", "ui", "06-projects.png"),
  palette: path.join(rootDir, "presentation", "assets", "ui", "07-palette.png"),
  sourcePng: path.join(rootDir, "docs", "customer-presentation", "assets", "source-demo.png"),
  mosaicPng: path.join(rootDir, "docs", "customer-presentation", "assets", "mosaic-preview.png"),
  summaryJson: path.join(rootDir, "docs", "customer-presentation", "assets", "summary.json"),
  materialsCsv: path.join(rootDir, "docs", "customer-presentation", "assets", "materials.csv"),
};

function has(filePath) {
  return fs.existsSync(filePath);
}

function loadSummary() {
  const fallback = {
    generated_at_utc: new Date().toISOString(),
    rows: 117,
    columns: 184,
    total_chips: 21528,
    actual_colors_used: 7,
    total_price: 38514,
    currency: "RUB",
  };

  if (!has(assets.summaryJson)) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(fs.readFileSync(assets.summaryJson, "utf-8"));
    return { ...fallback, ...parsed };
  } catch {
    return fallback;
  }
}

function loadTopColors(limit = 5) {
  if (!has(assets.materialsCsv)) {
    return [];
  }

  const lines = fs
    .readFileSync(assets.materialsCsv, "utf-8")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  if (lines.length <= 1) {
    return [];
  }

  return lines
    .slice(1)
    .map((line) => line.split(","))
    .map((parts) => ({
      name: parts[1] ?? "",
      ral: parts[2] ?? "",
      hex: parts[3] ?? "#999999",
      cells: Number(parts[4] ?? 0),
      ratio: Number(parts[5] ?? 0),
    }))
    .filter((row) => Number.isFinite(row.cells))
    .sort((a, b) => b.cells - a.cells)
    .slice(0, limit);
}

function fmtInt(value) {
  return new Intl.NumberFormat("ru-RU").format(value);
}

function fmtMoney(value, currency) {
  try {
    return new Intl.NumberFormat("ru-RU", {
      style: "currency",
      currency: (currency || "RUB").toUpperCase(),
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${fmtInt(value)} ${currency || "RUB"}`;
  }
}

function initDeck() {
  const pptx = new pptxgen();
  pptx.layout = "LAYOUT_WIDE";
  pptx.author = "Mozaika Team";
  pptx.company = "Mozaika";
  pptx.subject = "Расширенная презентация функционала";
  pptx.title = "Mozaika Studio - функциональная презентация";
  pptx.lang = "ru-RU";
  return pptx;
}

function addBase(slide, dark = false) {
  slide.background = { color: dark ? C.bgDark : C.bg };
  if (!dark) {
    slide.addShape(SHAPE.rect, {
      x: 0,
      y: 0,
      w: 13.333,
      h: 0.42,
      line: { color: "EAF1F8" },
      fill: { color: "EAF1F8" },
    });
    slide.addShape(SHAPE.rect, {
      x: 10.0,
      y: 0,
      w: 3.333,
      h: 0.42,
      line: { color: "D9F5F1" },
      fill: { color: "D9F5F1" },
    });
  }
}

function addHeader(slide, title, subtitle, dark = false) {
  slide.addText(title, {
    x: 0.56,
    y: 0.5,
    w: 12.1,
    h: 0.54,
    fontFace: "Segoe UI",
    fontSize: 30,
    bold: true,
    color: dark ? C.white : C.title,
  });
  if (subtitle) {
    slide.addText(subtitle, {
      x: 0.56,
      y: 1.03,
      w: 12.1,
      h: 0.3,
      fontFace: "Segoe UI",
      fontSize: 13,
      color: dark ? "CBD5E1" : C.muted,
    });
  }
}

function addBrowser(slide, options) {
  const {
    imagePath,
    x,
    y,
    w,
    h,
    caption = "",
    url = "https://mozaika.local/studio",
  } = options;

  const top = 0.33;
  const iy = y + top + 0.02;
  const ih = h - top - 0.04;

  slide.addShape(SHAPE.roundRect, {
    x,
    y,
    w,
    h,
    rectRadius: 0.05,
    line: { color: "C7D4E2", pt: 1 },
    fill: { color: "FFFFFF" },
    shadow: { color: "B4C4D6", angle: 45, blur: 4, distance: 3, opacity: 0.25 },
  });

  slide.addShape(SHAPE.rect, {
    x,
    y,
    w,
    h: top,
    line: { color: "E2E8F0", pt: 0.5 },
    fill: { color: "F8FAFC" },
  });

  slide.addShape(SHAPE.ellipse, {
    x: x + 0.12,
    y: y + 0.1,
    w: 0.08,
    h: 0.08,
    line: { color: "FCA5A5", pt: 0.5 },
    fill: { color: "F87171" },
  });
  slide.addShape(SHAPE.ellipse, {
    x: x + 0.23,
    y: y + 0.1,
    w: 0.08,
    h: 0.08,
    line: { color: "FCD34D", pt: 0.5 },
    fill: { color: "FBBF24" },
  });
  slide.addShape(SHAPE.ellipse, {
    x: x + 0.34,
    y: y + 0.1,
    w: 0.08,
    h: 0.08,
    line: { color: "86EFAC", pt: 0.5 },
    fill: { color: "4ADE80" },
  });

  slide.addShape(SHAPE.roundRect, {
    x: x + 0.55,
    y: y + 0.09,
    w: w - 0.7,
    h: 0.14,
    rectRadius: 0.03,
    line: { color: "D9E2EC", pt: 0.5 },
    fill: { color: "FFFFFF" },
  });
  slide.addText(url, {
    x: x + 0.63,
    y: y + 0.095,
    w: w - 0.9,
    h: 0.13,
    fontFace: "Segoe UI",
    fontSize: 8,
    color: "62748A",
  });

  if (has(imagePath)) {
    slide.addImage({
      path: imagePath,
      x: x + 0.02,
      y: iy,
      w: w - 0.04,
      h: ih,
      sizing: {
        type: "cover",
        x: x + 0.02,
        y: iy,
        w: w - 0.04,
        h: ih,
      },
    });
  }

  if (caption) {
    slide.addText(caption, {
      x,
      y: y + h + 0.04,
      w,
      h: 0.24,
      fontFace: "Segoe UI",
      fontSize: 10,
      color: C.muted,
      align: "center",
    });
  }
}

function addCard(slide, options) {
  const { x, y, w, h, title, value, valueColor = C.title } = options;
  slide.addShape(SHAPE.roundRect, {
    x,
    y,
    w,
    h,
    rectRadius: 0.05,
    line: { color: C.border, pt: 0.8 },
    fill: { color: C.panel },
  });
  slide.addText(title, {
    x: x + 0.12,
    y: y + 0.1,
    w: w - 0.24,
    h: 0.2,
    fontFace: "Segoe UI",
    fontSize: 10,
    color: C.muted,
  });
  slide.addText(value, {
    x: x + 0.12,
    y: y + 0.32,
    w: w - 0.24,
    h: 0.34,
    fontFace: "Segoe UI",
    fontSize: 17,
    bold: true,
    color: valueColor,
  });
}

function addBullets(slide, items, options) {
  const { x, y, w, lineHeight = 0.36, color = C.text, bulletColor = C.brand } = options;
  let top = y;
  for (const item of items) {
    slide.addShape(SHAPE.ellipse, {
      x,
      y: top + 0.07,
      w: 0.08,
      h: 0.08,
      line: { color: bulletColor, pt: 0.4 },
      fill: { color: bulletColor },
    });
    slide.addText(item, {
      x: x + 0.14,
      y: top,
      w: w - 0.14,
      h: 0.28,
      fontFace: "Segoe UI",
      fontSize: 12,
      color,
    });
    top += lineHeight;
  }
}
function buildDeck() {
  const summary = loadSummary();
  const topColors = loadTopColors(5);
  const generatedAt = new Date(summary.generated_at_utc).toLocaleString("ru-RU");

  const pptx = initDeck();

  // 1. Титул
  {
    const slide = pptx.addSlide();
    addBase(slide, true);

    slide.addShape(SHAPE.roundRect, {
      x: -1.7,
      y: -1.2,
      w: 6.2,
      h: 4.6,
      rectRadius: 1.4,
      line: { color: C.bgDark },
      fill: { color: "11406C", transparency: 30 },
    });
    slide.addShape(SHAPE.roundRect, {
      x: 8.6,
      y: -0.8,
      w: 5.8,
      h: 4.2,
      rectRadius: 1.1,
      line: { color: C.bgDark },
      fill: { color: "0EA5A4", transparency: 35 },
    });

    slide.addText("Mozaika Studio", {
      x: 0.62,
      y: 0.72,
      w: 6.1,
      h: 0.8,
      fontFace: "Segoe UI",
      fontSize: 38,
      bold: true,
      color: C.white,
    });
    slide.addText("Расширенная презентация функционала", {
      x: 0.62,
      y: 1.45,
      w: 6.2,
      h: 0.34,
      fontFace: "Segoe UI",
      fontSize: 14,
      color: "D5E4F8",
    });

    addBrowser(slide, {
      imagePath: assets.generated,
      x: 6.68,
      y: 0.64,
      w: 6.02,
      h: 4.2,
      caption: "Живой экран генерации мозаики",
    });

    addCard(slide, {
      x: 0.62,
      y: 2.35,
      w: 2.95,
      h: 1.02,
      title: "Всего чипов",
      value: fmtInt(summary.total_chips),
      valueColor: "D9F7FF",
    });
    addCard(slide, {
      x: 3.72,
      y: 2.35,
      w: 2.95,
      h: 1.02,
      title: "Цветов в версии",
      value: fmtInt(summary.actual_colors_used),
      valueColor: "DCFCE7",
    });
    addCard(slide, {
      x: 0.62,
      y: 3.5,
      w: 6.05,
      h: 1.02,
      title: "Стоимость текущей конфигурации",
      value: fmtMoney(summary.total_price, summary.currency),
      valueColor: "FEF3C7",
    });
    slide.addText(`Демо-прогон: ${generatedAt}`, {
      x: 0.62,
      y: 4.74,
      w: 5.5,
      h: 0.2,
      fontFace: "Segoe UI",
      fontSize: 10,
      color: "C6D6EA",
    });
  }

  // 2. Карта функционала
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "Карта функционала", "Что именно система умеет на текущем этапе");

    const blocks = [
      "Авторизация и роли",
      "Генерация мозаики",
      "Палитра и ручные правки",
      "Ценообразование",
      "Проекты и версии",
      "Экспорт и производство",
    ];
    const positions = [
      [0.75, 1.7],
      [4.6, 1.7],
      [8.45, 1.7],
      [0.75, 3.2],
      [4.6, 3.2],
      [8.45, 3.2],
    ];

    for (let i = 0; i < blocks.length; i += 1) {
      const [x, y] = positions[i];
      slide.addShape(SHAPE.roundRect, {
        x,
        y,
        w: 3.25,
        h: 1.12,
        rectRadius: 0.06,
        line: { color: "CFE0EF", pt: 1 },
        fill: { color: "FFFFFF" },
      });
      slide.addText(`${i + 1}. ${blocks[i]}`, {
        x: x + 0.2,
        y: y + 0.4,
        w: 2.9,
        h: 0.3,
        fontFace: "Segoe UI",
        fontSize: 13,
        bold: true,
        color: C.text,
      });
    }

    addBrowser(slide, {
      imagePath: assets.dashboard,
      x: 2.1,
      y: 5.35,
      w: 9.1,
      h: 1.9,
      caption: "Главный экран после авторизации",
    });
  }

  // 3. Роли
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "1) Авторизация и роли", "Доступ разделен по бизнес-ролям");

    addBrowser(slide, {
      imagePath: assets.login,
      x: 0.75,
      y: 1.45,
      w: 6.05,
      h: 4.85,
      caption: "Экран входа",
      url: "https://mozaika.local/",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 7.05,
      y: 1.45,
      w: 5.5,
      h: 4.85,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Права доступа", {
      x: 7.32,
      y: 1.72,
      w: 2.7,
      h: 0.3,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 16,
      color: C.title,
    });
    addBullets(slide, [
      "Админ: полный доступ к палитре, проектам, настройкам.",
      "Заказчик: генерация, ручные правки, проекты, экспорт.",
      "Просмотр: только чтение без изменения данных.",
      "Сохранение и предзаказ доступны после входа.",
    ], { x: 7.32, y: 2.1, w: 4.95 });

    slide.addShape(SHAPE.roundRect, {
      x: 7.32,
      y: 4.35,
      w: 4.95,
      h: 1.15,
      rectRadius: 0.05,
      line: { color: "CFE8FB", pt: 1 },
      fill: { color: "EFF6FF" },
    });
    slide.addText("Демо-аккаунты:\nadmin/admin123\ncustomer/customer123\nviewer/viewer123", {
      x: 7.56,
      y: 4.57,
      w: 4.5,
      h: 0.75,
      fontFace: "Segoe UI",
      fontSize: 11,
      color: "1E3A8A",
    });
  }

  // 4. Параметры генерации
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "2) Подготовка генерации", "Рабочее поле в мм, чип, шов, ограничение цветов и смещение");

    addBrowser(slide, {
      imagePath: assets.studio,
      x: 0.75,
      y: 1.45,
      w: 8.2,
      h: 5.4,
      caption: "Вкладка «Генерация»",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 9.15,
      y: 1.45,
      w: 3.45,
      h: 5.4,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Ключевые параметры", {
      x: 9.35,
      y: 1.72,
      w: 2.9,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 14,
      color: C.title,
    });
    addBullets(slide, [
      "Ширина/высота поля (мм).",
      "Размер чипа (10x10 мм).",
      "Ширина шва (по умолчанию 2 мм).",
      "Максимум используемых цветов.",
      "Цвет затирки.",
      "Смещение мозаики X/Y (плавно).",
    ], { x: 9.35, y: 2.05, w: 2.95, lineHeight: 0.42 });
  }

  // 5. Генерация
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "3) Генерация результата", "После расчета сразу доступны превью, легенда и метрики");

    addBrowser(slide, {
      imagePath: assets.generated,
      x: 0.75,
      y: 1.45,
      w: 7.8,
      h: 5.4,
      caption: "Сгенерированная мозаика в рабочей зоне",
    });

    addCard(slide, {
      x: 8.8,
      y: 1.65,
      w: 3.75,
      h: 0.95,
      title: "Сетка",
      value: `${fmtInt(summary.columns)} x ${fmtInt(summary.rows)}`,
      valueColor: C.blue,
    });
    addCard(slide, {
      x: 8.8,
      y: 2.75,
      w: 3.75,
      h: 0.95,
      title: "Всего чипов",
      value: fmtInt(summary.total_chips),
      valueColor: C.brandDark,
    });
    addCard(slide, {
      x: 8.8,
      y: 3.85,
      w: 3.75,
      h: 0.95,
      title: "Использовано цветов",
      value: fmtInt(summary.actual_colors_used),
      valueColor: C.green,
    });
    addCard(slide, {
      x: 8.8,
      y: 4.95,
      w: 3.75,
      h: 0.95,
      title: "Стоимость",
      value: fmtMoney(summary.total_price, summary.currency),
      valueColor: C.amber,
    });
  }
  // 6. Палитра и ручные правки
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "4) Палитра и ручные правки", "Автоматика + ручной контроль цвета по всей мозаике");

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 1.45,
      w: 6.0,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Автоматические механики", {
      x: 1.0,
      y: 1.75,
      w: 3.4,
      h: 0.25,
      fontFace: "Segoe UI",
      fontSize: 15,
      bold: true,
      color: C.title,
    });
    addBullets(slide, [
      "Первая генерация использует все нужные цвета из палитры.",
      "Параметр «Макс. цветов» сокращает палитру автоматически.",
      "Можно исключить конкретные цвета и пересчитать результат.",
      "Каждый чип получает один цвет из базы доступных оттенков.",
    ], { x: 1.0, y: 2.1, w: 5.45, lineHeight: 0.43 });

    slide.addShape(SHAPE.roundRect, {
      x: 6.95,
      y: 1.45,
      w: 5.65,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Ручные механики", {
      x: 7.22,
      y: 1.75,
      w: 2.9,
      h: 0.25,
      fontFace: "Segoe UI",
      fontSize: 15,
      bold: true,
      color: C.title,
    });
    addBullets(slide, [
      "Замена цвета A -> B применяется ко всей мозаике сразу.",
      "Смена цвета затирки после генерации.",
      "Zoom/Pan для проверки мелких деталей.",
      "Смещение изображения внутри поля с обрезкой по зоне.",
    ], { x: 7.22, y: 2.1, w: 5.1, lineHeight: 0.43 });
  }

  // 7. Цена
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "5) Цена и экономика", "Стоимость меняется в реальном времени при изменении цветов");

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 1.45,
      w: 7.9,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Как заказчик управляет бюджетом", {
      x: 1.0,
      y: 1.75,
      w: 3.8,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 15,
      color: C.title,
    });
    addBullets(slide, [
      "Уменьшает количество цветов - цена снижается.",
      "Сразу видит визуальный компромисс по детализации.",
      "Сравнивает версии и выбирает оптимальную.",
      "Фиксирует нужную версию в проекте.",
    ], { x: 1.0, y: 2.1, w: 7.45, lineHeight: 0.44 });

    addCard(slide, {
      x: 1.0,
      y: 4.6,
      w: 2.3,
      h: 1.0,
      title: "Чипов",
      value: fmtInt(summary.total_chips),
      valueColor: C.brandDark,
    });
    addCard(slide, {
      x: 3.45,
      y: 4.6,
      w: 2.3,
      h: 1.0,
      title: "Цветов",
      value: fmtInt(summary.actual_colors_used),
      valueColor: C.green,
    });
    addCard(slide, {
      x: 5.9,
      y: 4.6,
      w: 2.5,
      h: 1.0,
      title: "Текущая цена",
      value: fmtMoney(summary.total_price, summary.currency),
      valueColor: C.amber,
    });

    slide.addShape(SHAPE.roundRect, {
      x: 8.9,
      y: 1.45,
      w: 3.7,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Топ-цвета (пример)", {
      x: 9.15,
      y: 1.75,
      w: 3.1,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 14,
      color: C.title,
    });

    if (topColors.length === 0) {
      slide.addText("Нет данных materials.csv", {
        x: 9.15,
        y: 2.2,
        w: 3.1,
        h: 0.3,
        fontFace: "Segoe UI",
        fontSize: 11,
        color: C.muted,
      });
    } else {
      topColors.forEach((row, index) => {
        const y = 2.15 + index * 0.68;
        slide.addShape(SHAPE.roundRect, {
          x: 9.15,
          y: y + 0.02,
          w: 0.2,
          h: 0.2,
          rectRadius: 0.03,
          line: { color: "B9C6D6", pt: 0.6 },
          fill: { color: row.hex.replace("#", "") },
        });
        slide.addText(row.name, {
          x: 9.42,
          y,
          w: 2.05,
          h: 0.2,
          fontFace: "Segoe UI",
          fontSize: 9,
          color: C.text,
        });
        slide.addText(`${fmtInt(row.cells)} чипов`, {
          x: 11.47,
          y,
          w: 0.95,
          h: 0.2,
          fontFace: "Segoe UI",
          fontSize: 9,
          color: C.muted,
          align: "right",
        });
        slide.addText(`${row.ratio.toFixed(2)}%`, {
          x: 11.47,
          y: y + 0.2,
          w: 0.95,
          h: 0.2,
          fontFace: "Segoe UI",
          fontSize: 9,
          color: C.blue,
          align: "right",
        });
      });
    }
  }

  // 8. Превью
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "6) Превью и контроль качества", "Масштабирование и навигация перед выгрузкой");

    addBrowser(slide, {
      imagePath: assets.generated,
      x: 0.75,
      y: 1.45,
      w: 8.35,
      h: 5.45,
      caption: "Zoom/Pan и визуальная проверка",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 9.35,
      y: 1.45,
      w: 3.25,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Что проверяем", {
      x: 9.58,
      y: 1.75,
      w: 2.8,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 14,
      color: C.title,
    });
    addBullets(slide, [
      "Сохранение пропорций исходника.",
      "Ровные квадратные чипы.",
      "Артефакты после сокращения цветов.",
      "Читаемость контуров ключевых объектов.",
      "Готовность версии к согласованию.",
    ], { x: 9.58, y: 2.1, w: 2.75, lineHeight: 0.43 });
  }

  // 9. Проекты
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "7) Проекты и история версий", "Любую итерацию можно сохранить и восстановить");

    addBrowser(slide, {
      imagePath: assets.projects,
      x: 0.75,
      y: 1.45,
      w: 8.2,
      h: 5.45,
      caption: "Раздел «Проекты»",
      url: "https://mozaika.local/projects",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 9.15,
      y: 1.45,
      w: 3.45,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Что хранится", {
      x: 9.37,
      y: 1.75,
      w: 3.0,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 14,
      color: C.title,
    });
    addBullets(slide, [
      "Название и описание проекта.",
      "История генераций и версии.",
      "Активная версия для продолжения.",
      "Shared-ссылки и предзаказы.",
    ], { x: 9.37, y: 2.1, w: 3.0, lineHeight: 0.43 });
  }
  // 10. Шаринг и предзаказ
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "8) Шаринг и предварительный заказ", "От согласования макета к рабочему бизнес-процессу");

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 1.45,
      w: 12.0,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });

    const flow = [
      "Сохраняем подходящую версию",
      "Создаем shared-ссылку",
      "Отправляем предзаказ",
      "Меняем статус заказа",
      "Готовим выгрузку в производство",
    ];

    for (let i = 0; i < flow.length; i += 1) {
      const x = 1.15 + i * 2.25;
      slide.addShape(SHAPE.roundRect, {
        x,
        y: 2.2,
        w: 2.0,
        h: 1.4,
        rectRadius: 0.05,
        line: { color: "CCE0F0", pt: 1 },
        fill: { color: i % 2 === 0 ? "F8FCFF" : "FFFFFF" },
      });
      slide.addText(`${i + 1}`, {
        x: x + 0.12,
        y: 2.34,
        w: 0.2,
        h: 0.2,
        fontFace: "Segoe UI",
        fontSize: 12,
        bold: true,
        color: C.brandDark,
      });
      slide.addText(flow[i], {
        x: x + 0.12,
        y: 2.58,
        w: 1.74,
        h: 0.75,
        fontFace: "Segoe UI",
        fontSize: 11,
        color: C.text,
      });
      if (i < flow.length - 1) {
        slide.addShape(SHAPE.line, {
          x: x + 2.02,
          y: 2.92,
          w: 0.2,
          h: 0,
          line: { color: "9BB7D3", pt: 1.2 },
        });
      }
    }

    addBullets(slide, [
      "Заказчик получает ссылку и результат без технических сложностей.",
      "Менеджер фиксирует статусы и комментарии по согласованию.",
      "После подтверждения используется та же версия для экспорта.",
    ], { x: 1.1, y: 4.45, w: 11.35, lineHeight: 0.44 });
  }

  // 11. Палитра (админ)
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "9) Управление палитрой и RAL", "Цвета чипов и затирки ведутся централизованно");

    addBrowser(slide, {
      imagePath: assets.palette,
      x: 0.75,
      y: 1.45,
      w: 8.35,
      h: 5.45,
      caption: "Раздел «Палитра»",
      url: "https://mozaika.local/palette",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 9.35,
      y: 1.45,
      w: 3.25,
      h: 5.45,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Админ-функции", {
      x: 9.58,
      y: 1.75,
      w: 2.8,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 14,
      color: C.title,
    });
    addBullets(slide, [
      "Добавление цвета: название, RAL, HEX.",
      "Пакетный импорт палитры.",
      "Отдельная палитра затирки.",
      "Деактивация устаревших оттенков.",
      "Единая база цветов для проектов.",
    ], { x: 9.58, y: 2.1, w: 2.75, lineHeight: 0.43 });
  }

  // 12. Экспорт
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "10) Экспорт и печать", "Клиентские и производственные форматы в один клик");

    addBrowser(slide, {
      imagePath: assets.exportBlock,
      x: 0.75,
      y: 1.45,
      w: 7.95,
      h: 3.6,
      caption: "Блок «Экспорт и печать»",
    });

    addBrowser(slide, {
      imagePath: assets.mosaicPng,
      x: 8.95,
      y: 1.45,
      w: 3.65,
      h: 3.6,
      caption: "Пример результата",
      url: "export://mosaic-preview.png",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 5.25,
      w: 11.85,
      h: 1.65,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    addBullets(slide, [
      "PNG / JPEG / SVG / PDF: согласование визуала.",
      "materials.csv: ведомость материалов (цвет -> количество).",
      "grid.csv: координаты ряд/колонка для сборки.",
      "modules.csv + assembly-kit.pdf: поквадратная раскладка.",
    ], { x: 1.0, y: 5.45, w: 11.4, lineHeight: 0.35 });
  }

  // 13. Производственный комплект
  {
    const slide = pptx.addSlide();
    addBase(slide);
    addHeader(slide, "11) Производственный комплект", "Что получает заказчик и что уходит в цех");

    addBrowser(slide, {
      imagePath: assets.sourcePng,
      x: 0.75,
      y: 1.6,
      w: 3.8,
      h: 2.9,
      caption: "Исходное изображение",
      url: "input://source-demo.png",
    });
    addBrowser(slide, {
      imagePath: assets.mosaicPng,
      x: 4.75,
      y: 1.6,
      w: 3.8,
      h: 2.9,
      caption: "Сгенерированная мозаика",
      url: "output://mosaic-preview.png",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 8.75,
      y: 1.6,
      w: 3.85,
      h: 2.9,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Для цеха и монтажников", {
      x: 9.02,
      y: 1.86,
      w: 3.2,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 13,
      color: C.title,
    });
    addBullets(slide, [
      "Нумерация модулей.",
      "Координатная раскладка.",
      "Легенда цветов.",
      "Зеркальный режим печати.",
    ], { x: 9.02, y: 2.2, w: 3.2, lineHeight: 0.36 });

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 4.75,
      w: 11.85,
      h: 2.15,
      rectRadius: 0.06,
      line: { color: C.border, pt: 1 },
      fill: { color: C.panel },
    });
    slide.addText("Итог по ТЗ v1", {
      x: 1.0,
      y: 4.98,
      w: 2.0,
      h: 0.25,
      fontFace: "Segoe UI",
      bold: true,
      fontSize: 14,
      color: C.title,
    });
    addBullets(slide, [
      "Сценарий «загрузил -> сгенерировал -> оптимизировал -> сохранил -> выгрузил» реализован.",
      "Роли, история генераций, shared-ссылки и предзаказы включены.",
      "Ключевые форматы выгрузки для согласования и производства доступны.",
      "Платформа готова к расширению (новые размеры чипа, параметры модулей).",
    ], { x: 1.0, y: 5.28, w: 11.4, lineHeight: 0.38 });
  }

  // 14. Финал
  {
    const slide = pptx.addSlide();
    addBase(slide, true);
    addHeader(slide, "Финал", "Система готова к демонстрациям и пилотным заказам", true);

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 1.45,
      w: 12.0,
      h: 3.9,
      rectRadius: 0.08,
      line: { color: "29406A", pt: 1 },
      fill: { color: "0F1B33", transparency: 8 },
    });
    addBullets(slide, [
      "Понятный браузерный интерфейс для заказчика и менеджера.",
      "Наглядная проверка результата до старта производства.",
      "Прямое управление балансом «детализация / цена».",
      "Готовые производственные выгрузки без ручной подготовки.",
      "База проектов, версий и ролей для коммерческой эксплуатации.",
    ], {
      x: 1.02,
      y: 1.85,
      w: 11.4,
      lineHeight: 0.45,
      color: "D8E4F5",
      bulletColor: "5EEAD4",
    });

    slide.addShape(SHAPE.roundRect, {
      x: 0.75,
      y: 5.65,
      w: 12.0,
      h: 1.1,
      rectRadius: 0.06,
      line: { color: "2A597C", pt: 1 },
      fill: { color: "10374C" },
    });
    slide.addText("Следующий шаг: живое демо и фиксация финальных производственных параметров.", {
      x: 1.0,
      y: 5.98,
      w: 11.5,
      h: 0.26,
      fontFace: "Segoe UI",
      fontSize: 15,
      bold: true,
      color: "ECFEFF",
      align: "center",
    });
  }

  return pptx;
}

const deck = buildDeck();
await deck.writeFile({ fileName: outPath });
console.log(`Презентация сохранена: ${outPath}`);
