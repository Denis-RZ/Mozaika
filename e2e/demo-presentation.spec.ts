import { test, expect } from "@playwright/test";
import path from "path";

const DEMO_USER = "admin";
const DEMO_PASSWORD = "admin123";
const STEP_PAUSE_MS = 1500;

test.describe("Интерактивная презентация: как пользоваться Mozaika", () => {
  test("Полный сценарий для заказчика: вход, студия, проекты, экспорт", async ({
    page,
  }) => {
    // ——— Шаг 1: Открытие приложения и вход ———
    await test.step("1. Открытие приложения", async () => {
      await page.goto("/");
      await expect(page.getByRole("heading", { name: /вход в студию/i })).toBeVisible();
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    await test.step("2. Вход в систему (демо-аккаунт)", async () => {
      await page.getByPlaceholder("Логин").fill(DEMO_USER);
      await page.waitForTimeout(300);
      await page.getByPlaceholder("Пароль").fill(DEMO_PASSWORD);
      await page.waitForTimeout(300);
      await page.getByRole("button", { name: /войти/i }).click();
      await expect(page.getByText(/Студия Mozaika/i).first()).toBeVisible({ timeout: 10000 });
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    // ——— Шаг 2: Вкладка «Генерация» ———
    await test.step("3. Вкладка «Генерация» — загрузка изображения и параметры", async () => {
      await page.getByRole("button", { name: "Генерация" }).click();
      await expect(page.getByRole("button", { name: "Сгенерировать мозаику" })).toBeVisible();
      const fixturesDir = path.join(process.cwd(), "fixtures");
      const sampleJpg = path.join(fixturesDir, "sample.jpg");
      const samplePng = path.join(fixturesDir, "sample.png");
      try {
        const fs = await import("fs");
        const pathToUse = fs.existsSync(sampleJpg) ? sampleJpg : samplePng;
        if (fs.existsSync(pathToUse)) {
          const input = page.locator('input[type="file"][accept="image/*"]');
          await input.setInputFiles(pathToUse);
          await page.waitForTimeout(STEP_PAUSE_MS);
        }
      } catch {
        // без файла продолжаем — показываем интерфейс
      }
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    await test.step("4. Генерация мозаики (если загружено изображение)", async () => {
      const btn = page.getByRole("button", { name: /Сгенерировать мозаику/i });
      await btn.scrollIntoViewIfNeeded();
      if (await btn.isEnabled()) {
        await btn.click();
        await page.waitForTimeout(5000);
        await expect(page.getByRole("heading", { name: "Превью" })).toBeVisible();
      }
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    // ——— Шаг 3: Вкладка «Проекты» ———
    await test.step("5. Вкладка «Проекты» — список и сохранение", async () => {
      await page.getByRole("button", { name: "Проекты" }).click();
      await expect(page.getByRole("button", { name: /Обновить список/i })).toBeVisible({
        timeout: 8000,
      });
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    await test.step("6. Палитра цветов", async () => {
      await page.getByRole("button", { name: "Палитра" }).click();
      await expect(page.getByRole("heading", { name: /Добавить цвет палитры/i })).toBeVisible({
        timeout: 5000,
      });
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    // ——— Возврат в студию и блок «Экспорт» ———
    await test.step("7. Экспорт и печать (блок в студии)", async () => {
      await page.getByRole("button", { name: "Генерация" }).click();
      await page.waitForTimeout(800);
      const exportSummary = page.locator('summary').filter({ hasText: /Экспорт и печать/i });
      if (await exportSummary.isVisible()) {
        await exportSummary.click();
        await page.waitForTimeout(STEP_PAUSE_MS);
        await expect(page.getByText(/DPI|PNG|PDF|экспорт/i).first()).toBeVisible();
      }
      await page.waitForTimeout(STEP_PAUSE_MS);
    });

    await test.step("8. Завершение презентации", async () => {
      await expect(page.getByText(/Студия Mozaika|Пользователь/i).first()).toBeVisible();
    });
  });
});
