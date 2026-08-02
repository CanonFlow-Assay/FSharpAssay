const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    
    page.on('console', msg => console.log('LOG:', msg.text()));
    page.on('pageerror', error => console.log('ERROR:', error.message));
    page.on('requestfailed', request =>
      console.log('REQUEST FAILED:', request.url(), request.failure()?.errorText)
    );
    
    console.log('Navigating...');
    await page.goto('https://canonflowfoundation.github.io/FSharpAssay/?v=' + Date.now(), { waitUntil: 'networkidle' });
    
    try {
        await page.waitForSelector('text=F# Code', { timeout: 15000 });
        console.log("SUCCESS!");
    } catch (e) {
        console.log('Timeout. Current content:');
        console.log(await page.content());
    }
    
    await browser.close();
})();
