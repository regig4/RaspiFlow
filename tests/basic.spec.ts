import { test, expect } from '@playwright/test';

test.describe('Ask AI Endpoint', () => {
  test('should respond with a non-empty message for the provided input', async ({ page }) => {
    // Navigate to the /askAi endpoint with the input parameter
    await page.goto('http://localhost:5029/askAi?input=exampleText');

    // Verify the response message is not empty
    const responseMessage = await page.textContent('body');
    expect(responseMessage).not.toBeNull();
    expect(responseMessage?.trim().length).toBeGreaterThan(0);
  });
});