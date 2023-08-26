' Script that runs Playnite, and potentially a Playnite game with focus, 
' and prevents Steam from waiting on Playnite to close (due to misidentifying it as a game process).
' This script is intended to be run by GlosSITarget when started via Steam.
' If a game should be started, the Playnite database ID of the game must be provided as the argument. 
' Otherwise only Playnite will be started.
' Note that multiple installations of Playnite is currently not supported.
' This is also the case for Playnite's own desktop shortcuts.

const strGeneralPlayniteArguments = "--nolibupdate --hidesplashscreen"
Set objWMIService = GetObject("winmgmts:")
Set objWSShell = CreateObject("WScript.Shell")

' Focus Playnite if it is already running. Ensures that the started game becomes focused.
FocusPlaynite()

' Get the Playnite start arguments.
If wscript.arguments.count > 0 Then
	strStartArguments = "--start " & WScript.Arguments(0)
ElseIf IsSteamBigPictureModeRunning() Then
	strStartArguments = "--startfullscreen"
Else
	strStartArguments = "--startdesktop"
End If

StartPlaynite(strGeneralPlayniteArguments & " " & strStartArguments)

' Start Playnite with the provided application arguments.
Function StartPlaynite(strArguments)
	' Configure the Playnite process.
	Set objStartup = objWMIService.Get("Win32_ProcessStartup")
	Set objConfig = objStartup.SpawnInstance_
	objConfig.ShowWindow = SW_NORMAL
	Set objProcess = objWMIService.Get("Win32_Process")

	' Start Playnite. By using the Create method, Steam will consider the started process as unrelated to GlosSI.
	intResult = objProcess.Create(GetPlayniteAppPath() & " " & strArguments, null, objConfig, intProcessId)

	If intResult Then
		Call MsgBox("Starting Playnite failed: error code " & intResult & ".", 16, _
			"Playnite GlosSI Integration Extension")
	End If
End Function

' Focuses Playnite if it is running.
Function FocusPlaynite()
	Set colPlayniteProcess = objWMIService.ExecQuery("Select * From Win32_Process Where " & _ 
		"name = 'Playnite.DesktopApp.exe' Or name = 'Playnite.FullscreenApp.exe'")
	If colPlayniteProcess.Count Then
		objWSShell.AppActivate(colPlayniteProcess.ItemIndex(0).ProcessID)
	End If
End Function


' Gets the path to the Playnite executable (with quotation marks).
Function GetPlayniteAppPath()
	' Read the registry to find the path to Playnite.
	strRegData = objWSShell.RegRead("HKEY_CLASSES_ROOT\Playnite\shell\open\command\")
	GetPlayniteAppPath = Mid(strRegData, 1, Len(strRegData) - Len(" --uridata ""%1"""))
End Function

' Gets the localized window title of the Steam Big Picture mode window.
Function GetSteamBigPictureModeWindowTitle()
	' Get the path to the Steam directory.
	strSteamPath = objWSShell.RegRead("HKEY_CURRENT_USER\SOFTWARE\Valve\Steam\SteamPath")
	' Get the Steam language.
	strSteamLanguage = objWSShell.RegRead("HKEY_CURRENT_USER\SOFTWARE\Valve\Steam\Language")

	' Read the Steam localization file that contains the window title.
	Set objFSO = CreateObject("Scripting.FileSystemObject")
	Set objFile = objFSO.OpenTextFile(strSteamPath & "/steamui/localization/steamui_" & _
		strSteamLanguage & "-json.js", 1)
	strFileContents = objFile.ReadAll()
	objFile.Close

	' Extract the window title.
	' Example string: "SP_WindowTitle_BigPicture":"Steam: Big Picture-modus"
	const strTarget = """SP_WindowTitle_BigPicture"":"""
	intTargetLen = Len(strTarget)
	lngTargetStartPos = InStrRev(strFileContents, strTarget)
	lngTargetEndPos = InStr(lngTargetStartPos + intTargetLen, strFileContents, """,""")
	GetSteamBigPictureModeWindowTitle = Mid(strFileContents, lngTargetStartPos + intTargetLen, _
		lngTargetEndPos - lngTargetStartPos - intTargetLen)
End Function

' Checks if Steam is currently in Big Picture mode.
Function IsSteamBigPictureModeRunning()
	' Since Win32 FindWindow() and EnumWindows() cannot be called in VBScript, TASKLIST is used instead.
	' It should currently not be necessary to escape the Steam window title.
	IsSteamBigPictureModeRunning = objWSShell.Run("CMD /V /C """ & _
		"TASKLIST /FI ""IMAGENAME eq steamwebhelper.exe"" /FI ""WINDOWTITLE eq " & _ 
		GetSteamBigPictureModeWindowTitle() & """ /FO CSV /NH | " & _
		"FIND """""""" & " & _
		"EXIT /B !ERRORLEVEL!""", 0, True) = 0
End Function