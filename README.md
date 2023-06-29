# ScriptVsNewWindow

Test cases for script injection when opening a new window and navigating to a URL (incl. POST navigation).

# Expected Behavior
## Common for all cases
The project loads an embedded web page with buttons and links. When any of them are clicked a new window should appear containing a WebView2 instance that:
  1. Has scripts added (via a call to `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync`).
  2. This can be verified using the automatically opened DevTools window that should show the "script injected - before setting NewWindow" line in the console.

Script injection is expected to work with any value of the `rel` attribute of the `<a>` and `<form>` elements used to open the new window.
I.e., it must not be required to set `rel="noopener"`.

## Case 1: Open Window From Javascript
The new window should display the DuckDuckGo search page.

## Case 2: Open Window From Javascript without navigation
The new window should display a button (Open Tab1) and the text "This content is generated purely by javascript"

## Case 3: Open Window From Target="_blank" (GET)
The new window should display the DuckDuckGo search page.

## Case 4: Start open window test with POST navigation
The main window should display the Lite version of the DDG search page with a search box containing "test" and a Search button.
The new window should display the search result page with actual results and the text box at the top should contain "test".


# Actual Behavior
Case 1 and Case 2 are OK.

Case 3 and Case 4: the line "script injected - before setting NewWindow" is not shown, which means that script injection is not working.

# Requirements
This project requires:
  1. .NET6 SDK
  2. WebView2 canary version. To test you need to install [Edge Canary](https://www.microsoftedgeinsider.com/en-us/download/canary).
