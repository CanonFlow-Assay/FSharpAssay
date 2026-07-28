const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();

    page.on('console', msg => console.log('LOG:', msg.text()));
    page.on('pageerror', error => console.log('ERROR:', error.message));
    page.on('requestfailed', request =>
      console.log('REQUEST FAILED:', request.url(), request.failure()?.errorText)
    );

    try {
        const publicUrl = `https://canonflowfoundation.github.io/FSharpAssay/?v=${Date.now()}`;
        let verified = false;

        for (let attempt = 1; attempt <= 5 && !verified; attempt++) {
            try {
                console.log(`Verification attempt ${attempt}: ${publicUrl}`);
                const response = await page.goto(publicUrl, {
                    waitUntil: 'domcontentloaded',
                    timeout: 60000
                });

                if (!response || !response.ok()) {
                    throw new Error(`Unexpected HTTP response: ${response?.status() ?? 'none'}`);
                }

                await page.waitForSelector('text=F# Code', { timeout: 60000 });
                verified = true;
                console.log('Public deployment verified successfully.');
            } catch (error) {
                console.error(`Attempt ${attempt} failed:`, error.message);
                if (attempt === 5) {
                    console.error('Final page content:', await page.content());
                    throw error;
                }
                await page.waitForTimeout(10000);
            }
        }
    } finally {
        await browser.close();
    }
})().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
