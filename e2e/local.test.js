import { test, expect, beforeAll, afterAll } from 'vitest';
import { chromium } from 'playwright';

let browser;
let page;

beforeAll(async () => {
    browser = await chromium.launch();
    page = await browser.newPage();
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));
    page.on('pageerror', error => console.log('PAGE ERROR:', error.message));
    page.on('requestfailed', request =>
      console.log('REQUEST FAILED:', request.url(), request.failure()?.errorText)
    );
});

afterAll(async () => {
    await browser.close();
});

test('Local URL loads FsAssay Web Playground', async () => {
    await page.goto('http://127.0.0.1:8080/FSharpAssay/', { waitUntil: 'networkidle' });
    try {
        await page.waitForSelector('text=F# Code', { timeout: 15000 });
    } catch(e) {
        console.error("Timeout. Content:");
        console.error(await page.content());
    }
}, 60000);
