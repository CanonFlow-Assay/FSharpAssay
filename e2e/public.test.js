import { test, expect, beforeAll, afterAll } from 'vitest';
import { chromium } from 'playwright';

let browser;
let page;

const PUBLIC_URL = 'https://canonflowfoundation.github.io/FSharpAssay/';

beforeAll(async () => {
    browser = await chromium.launch();
    page = await browser.newPage();
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));
    page.on('pageerror', error => console.log('PAGE ERROR:', error.message));
});

afterAll(async () => {
    await browser.close();
});

test('Public URL loads FsAssay Web Playground', async () => {
    console.log(`Navigating to ${PUBLIC_URL}...`);
    await page.goto(PUBLIC_URL, { waitUntil: 'networkidle' });

    try {
        await page.waitForSelector('text=F# Code', { timeout: 90000 });
    } catch (e) {
        console.error("Timeout waiting for load.");
        const content = await page.content();
        console.error("FULL PAGE CONTENT:", content);
        throw e;
    }
    
    const codeHeading = page.locator('text=F# Code').first();
    await expect(codeHeading).toBeVisible();

    console.log('Public URL verified successfully!');
}, 120000);
