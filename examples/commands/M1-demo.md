# Milestone 1 demo script

1. Launch Vayu Desktop (F5 in Visual Studio or `dotnet run --project apps/Vayu.Desktop`).
2. On the Home page, type: `open notepad`
3. Vayu shows the parsed plan:
   `Intent: app.launch  Args: { app: "notepad" }  Risk: L1`
4. L1 is auto-allowed. Notepad opens.
5. Open the Logs page — a new row exists:
   `… app.launch  L1  Allowed  Success`
6. Type: `open downloads`
7. Explorer opens to your Downloads folder. Logs page updates.
8. Type: `show settings`
9. The Settings page navigates open. Confirm AI mode is `Offline` (M1 default).
10. Open Security page — confirm it says **Gemini key: not configured**.
11. Stop the app. Re-run. The Logs page repopulates from the SQLite `Actions` table.

If all 11 steps pass and CI is green, M1 is done.
