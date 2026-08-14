; DeepSeek Harness 一键安装包（NSIS 外壳）
; 作用：把「安装向导 + 全部离线依赖」打包成单个 setup.exe，解压后自动拉起向导

Unicode True
SetCompressor /SOLID zlib
Name "DeepSeek Harness 一键安装"
OutFile "DeepSeek-Harness-Setup.exe"
InstallDir "$LOCALAPPDATA\DSHInstaller"
RequestExecutionLevel user
CRCCheck on

!include "MUI2.nsh"
!include "LogicLib.nsh"

; 源码根目录：CI 中来自 GITHUB_WORKSPACE 环境变量（编译期求值）
!ifndef SRCDIR
  !define SRCDIR "$%GITHUB_WORKSPACE%"
!endif

!define MUI_ICON "${SRCDIR}\assets\icon.ico"
!define MUI_UNICON "${SRCDIR}\assets\icon.ico"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE "欢迎使用 DeepSeek Harness 一键安装包"
!define MUI_WELCOMEPAGE_TEXT "本安装包将把 DeepSeek Harness（DeepSeek 官方的 AI 智能体工作台）以及运行它所需的全部文件，一次性安装到你的电脑。$\r$\n$\r$\n整个过程不需要联网、不需要懂任何技术，跟着提示点「下一步」即可。$\r$\n$\r$\n安装完成后会自动打开精美的安装向导，引导你完成最后的配置。"

!define MUI_DIRECTORYPAGE_TEXT_TOP "安装包本身将被解压到下面的文件夹（程序本体安装位置由向导决定）。直接点「下一步」即可。"

!define MUI_FINISHPAGE_TITLE "解压完成"
!define MUI_FINISHPAGE_TEXT "安装包文件已就绪。$\r$\n即将自动打开「一键安装向导」，完成最后的安装配置。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\install\install.exe"
!define MUI_FINISHPAGE_RUN_TEXT "立即运行安装向导"
!define MUI_FINISHPAGE_RUN_NOTCHECKED

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"

Section "Install" SEC_MAIN
  SetOutPath "$INSTDIR\install"
  File /r "${SRCDIR}\dist\install\*.*"

  SetOutPath "$INSTDIR\install\_assets"
  File "${SRCDIR}\_assets\node.zip"
  File "${SRCDIR}\_assets\dsh.zip"
  File "${SRCDIR}\_assets\switch.zip"
  File "${SRCDIR}\_assets\bundle_info.json"
  File "${SRCDIR}\_assets\checksums.json"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
SectionEnd
