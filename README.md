# RunOnlyWhenIdle
Kills another process when pc is used and runs it again when there is no user input for 10 minutes (doesn't run if wasn't killed).

## Usage
RunOnlyWhenIdle.exe process-path [start-path [start-args]]

Usable for Dropbox when you don't want it to sync and lock files while you are at place.
## Example
1. Start Dropbox
2. Execute RunOnlyWhenIdle.exe "C:\Program Files (x86)\Dropbox\Client\Dropbox.exe"
3. Dropbox will be killed after 30 seconds
4. Wait 10 minutes without touching anything
5. Dropbox will be restarted

