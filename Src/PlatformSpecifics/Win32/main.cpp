///////////////////////////////////////////////////////////////////////
//
//	Device Specific Defines
//
///////////////////////////////////////////////////////////////////////

#include "stdafx.h"

#include "Core/irrlicht.h"

#include <windows.h>
#include <direct.h>
#include <shlobj.h>
#include <cstdlib>
#include <string>

#include "Core/Application.h"

#include "IO/TouchScreen/TouchPad.h"
#include "IO/Accelerometer/AccelerometerWin32.h"


#include "Editor/GS_Editor.h"
#include "Editor/ExternalEditor.h"
#include "Fonts/FontManager.h"

#include "Game/Playfield.h"
#include "Game/Objects/Unit.h"
#include "Game/Achievements.h"

///////////////////////////////////////////////////////////////////////

#define START_APPLICATION_LEFT_MONITOR	0

#define START_APPLICATION_IN_WIDESCREEN	true
#define WINDOW_TITLE					L"FortressDefender - PC emu"

///////////////////////////////////////////////////////////////////////

bool g_visualizeWaves = false;

class ResizingWindow : public IEventReceiver
{
private:
	bool m_wide;
	bool m_frozen;
	mutable bool m_showOne;
public:
	static const int X_PADDING = 8;
	static const int Y_PADDING = 27;
	int mouseX, mouseY;
private:
	ResizingWindow() : m_wide(START_APPLICATION_IN_WIDESCREEN), m_frozen(false) {};

public:
	static ResizingWindow* inst() {static ResizingWindow rw; return &rw;};
	virtual ~ResizingWindow() {};

	bool isFrozen() const
	{
		if(m_frozen && m_showOne)
		{
			m_showOne = false;
			return false;
		}

		return m_frozen;
	};

	void updateWindow()
	{
		HWND w = FindWindowW(NULL, WINDOW_TITLE);
		if(w)
		{
			WINDOWPLACEMENT p = {};
			p.length = sizeof(p);
			GetWindowPlacement(w, &p);

			if(m_wide)	{MoveWindow(w, p.rcNormalPosition.left, p.rcNormalPosition.top, 480+X_PADDING, 320+Y_PADDING, TRUE);}
			else		{MoveWindow(w, p.rcNormalPosition.left, p.rcNormalPosition.top, 320+X_PADDING, 480+Y_PADDING, TRUE);}

			((AccelerometerWin32*)Application::GetInstance()->GetAccelerometer())->setWide(m_wide);
		}
	};

	void UpdateTitle ()
	{
		wchar_t buf[256];
		swprintf(buf, 256, L"%ls - mouse:%d,%d speed:%d%%", WINDOW_TITLE, mouseX, mouseY, int(os::Timer::getSpeed() * 100.0f + 0.5f));
		__IRR_DEVICE()->setWindowCaption(buf);
	}

	bool OnEvent(const SEvent& ev)
	{
		if(ev.EventType == EET_KEY_INPUT_EVENT)
		{
			if( m_frozen )
			{
				if( ev.KeyInput.Key == VK_SPACE )
				{
					if( !ev.KeyInput.PressedDown )
					{
						m_showOne = true;
					}
				}
				else
				{
					m_frozen = false;
				}
			}
			else if(ev.KeyInput.Key == KEY_KEY_P && !ev.KeyInput.PressedDown)
			{
				m_frozen = true;
				m_showOne = false;
			}
			else if(ev.KeyInput.Key == VK_ESCAPE && !ev.KeyInput.PressedDown)
			{
				Application::GetInstance()->GetDevice()->closeDevice();
			}
			else if(ev.KeyInput.Key == VK_F1 && !ev.KeyInput.PressedDown)
			{
				m_wide = !m_wide;
				updateWindow();
			}
			else if(ev.KeyInput.Key == VK_F2 && !ev.KeyInput.PressedDown)
			{
				os::Timer::setSpeed(os::Timer::getSpeed() * (1.0f / 2.0f));
				printf("game speed = %d%%\n", int(os::Timer::getSpeed() * 100.0f + 0.5f));
				UpdateTitle();
			}
			else if(ev.KeyInput.Key == VK_F3 && !ev.KeyInput.PressedDown)
			{
				os::Timer::setSpeed(os::Timer::getSpeed() / (1.0f / 2.0f));
				printf("game speed = %d%%\n", int(os::Timer::getSpeed() * 100.0f + 0.5f));
				UpdateTitle();
			}
			else if(ev.KeyInput.Key == KEY_KEY_A && !ev.KeyInput.PressedDown)
			{
				__ACHIEVEMENTS()->setAchieved( "Addicted" );
			}
			else if((ev.KeyInput.Key == KEY_KEY_E || ev.KeyInput.Key == KEY_KEY_R) && !ev.KeyInput.PressedDown)
			{
				if (!g_editor)
				{
					GS_Editor::GetInstance()->enter();
				}
			}
			else if((ev.KeyInput.Key == VK_F5) && !ev.KeyInput.PressedDown)
			{
				CFontManager::GetInstance()->ReleaseFonts();
				CGameConfig::GetInstance()->importFile("config/fonts.xml");
				initUnitTypes();
			}
			else if((ev.KeyInput.Key == VK_F6) && !ev.KeyInput.PressedDown)
			{
				CPlayfield::GetInstance()->GetLevelStats().Gold = 
					MAX(0, CPlayfield::GetInstance()->GetLevelStats().Gold - 1000);
				CPlayfield::GetInstance()->GetLevelStats().Souls = 
					MAX(0, CPlayfield::GetInstance()->GetLevelStats().Souls - 10);
			}
			else if((ev.KeyInput.Key == VK_F7) && !ev.KeyInput.PressedDown)
			{
				CPlayfield::GetInstance()->GetLevelStats().Gold += 1000;
				CPlayfield::GetInstance()->GetLevelStats().Souls += 10;
			}
			else if((ev.KeyInput.Key == VK_F8) && !ev.KeyInput.PressedDown)
			{
				CPlayfield::GetInstance()->SetPlayState(CPlayfield::PS_Shop);
			}
			else if((ev.KeyInput.Key == VK_F9) && !ev.KeyInput.PressedDown)
			{
				g_visualizeWaves = !g_visualizeWaves;
			}
			else if(( ev.KeyInput.Key == VK_INSERT ) && !ev.KeyInput.PressedDown )
			{				
				if ( CTouchPad::s_currentDeviceOrientation == EOO_0 )
					CTouchPad::s_currentDeviceOrientation = EOO_180;
				else
					CTouchPad::s_currentDeviceOrientation = EOO_0;
				//__IRR_DRIVER()->setViewPort( rect<s32>(0,0,320,480 ) );
			}
			else if((ev.KeyInput.Key == VK_PAUSE) && !ev.KeyInput.PressedDown)
			{
				if( __APP()->isPaused() )
					__APP()->Resume();
				else
					__APP()->Pause();
					
			}
			}
		else if( ev.EventType == EET_MOUSE_INPUT_EVENT && !m_frozen )
		{
			static bool isDown = false;
			static s32 touchIdx = 0;

			switch(ev.MouseInput.Event)
			{
			case EMIE_LMOUSE_PRESSED_DOWN:
				isDown = true;
				CTouchPad::EventTouchDown( 0, ev.MouseInput.X, ev.MouseInput.Y );
				touchIdx = 0;
				break;

			case EMIE_LMOUSE_LEFT_UP:
				isDown = false;
				CTouchPad::EventTouchUp( 0, -1, -1 );
				break;

			case EMIE_MOUSE_MOVED:
				if( isDown )
					CTouchPad::EventTouchMove( touchIdx, ev.MouseInput.X, ev.MouseInput.Y );
				mouseX = ev.MouseInput.X;
				mouseY = ev.MouseInput.Y;
				UpdateTitle();
				break;

			case EMIE_RMOUSE_PRESSED_DOWN:
				isDown = true;
				CTouchPad::EventTouchDown( 1, ev.MouseInput.X, ev.MouseInput.Y );
				touchIdx = 1;
				break;

			case EMIE_RMOUSE_LEFT_UP:
				isDown = false;
				CTouchPad::EventTouchUp( 1, -1, -1 );
				break;
			}
		}

		__IRR_GUI()->postEventFromUser(ev);
		if (g_editor) g_editor->OnEvent(ev);

		return false;
	};
};

///////////////////////////////////////////////////////////////////////

//int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
int main(int argc, char* argv[])
{
    char executable[MAX_PATH];
    const DWORD length = GetModuleFileNameA(NULL, executable, MAX_PATH);
    if (!length || length >= MAX_PATH) return 1;
    std::string directory(executable, length);
    directory.resize(directory.find_last_of("/\\") + 1);
    std::string assets = CASTLE_ASSET_DIR;
    const std::string packaged = directory + "../assets";
    if (GetFileAttributesA(packaged.c_str()) != INVALID_FILE_ATTRIBUTES) assets = packaged;
    int frames = 0;
    std::string editorBridge, saveDirectory;
    for (int i = 1; i < argc; ++i) {
        if (!strcmp(argv[i], "--assets") && i + 1 < argc) assets = argv[++i];
        else if (!strcmp(argv[i], "--editor-bridge") && i + 1 < argc) editorBridge = argv[++i];
        else if (!strcmp(argv[i], "--save-dir") && i + 1 < argc) saveDirectory = argv[++i];
        else if (!strcmp(argv[i], "--frames") && i + 1 < argc) frames = atoi(argv[++i]);
        else { fprintf(stderr, "Usage: %s [--assets directory] [--frames count] [--editor-bridge directory] [--save-dir directory]\n", argv[0]); return 2; }
    }
    if (_chdir(assets.c_str()) != 0) {
        fprintf(stderr, "Could not open asset directory: %s\n", assets.c_str());
        return 1;
    }
    const char* localData = getenv("LOCALAPPDATA");
    const std::string save = !saveDirectory.empty() ? saveDirectory + "\\" : std::string(localData && *localData ? localData : directory.c_str()) + "\\CastleFrenzy\\";
    if (SHCreateDirectoryExA(NULL, (save + "profiles").c_str(), NULL) != ERROR_SUCCESS &&
        GetFileAttributesA((save + "profiles").c_str()) == INVALID_FILE_ATTRIBUTES) {
        fprintf(stderr, "Could not create save directory: %s\n", save.c_str());
        return 1;
    }

	IrrlichtDevice *device = createDevice(video::EDT_OPENGL, dimension2d<s32>(480, 320), 16, false, false, false, 0);
	if (!device) { fprintf(stderr, "Could not create an OpenGL window.\n"); return 1; }
	device->setWindowCaption(WINDOW_TITLE);
	device->setResizeAble(true);


	Application* app = __APP();
	app->setFilePaths("", "./", save.c_str());
	app->InitWin32(device);
	app->RegisterForIrrlichtEvents(ResizingWindow::inst());

	ResizingWindow::inst()->updateWindow();

#if START_APPLICATION_LEFT_MONITOR == 1

	HWND w = FindWindowW(NULL, WINDOW_TITLE);
	if(w)
	{
		WINDOWPLACEMENT p = {};
		p.length = sizeof(p);
		MoveWindow( w, -1100, 70, 960+ResizingWindow::X_PADDING, 640+ResizingWindow::Y_PADDING, TRUE );		
	}

#endif

	// end world app ...

	GS_Editor::GetInstance()->init();

	int frameCount = 0;
    ExternalEditor externalEditor(editorBridge);
	while(device->run())
	{
        externalEditor.update();
		if( !app->isPaused() && !externalEditor.paused() )
		{
			if (!g_editor)
			{
				if(!ResizingWindow::inst()->isFrozen())
				{
					app->Update();

					uiBegin(__IRR_GUI()->getRootGUIElement());
					if (g_visualizeWaves &&
						canVisualizeWaves(
						CPlayfield::GetInstance()->m_CurrentLevel,
						CPlayfield::GetInstance()->m_CurrentDay))
					{
						uiVisualizeWaves(CPlayfield::GetInstance()->getWaves().getLast().time + 2.0f,
							r2i(0, uiParentHeight() - 120, uiParentWidth(), uiParentHeight()));
					}
					uiEnd();
				}
				else
					app->Draw();
			}
			else
			{
				g_editor->update();
				if (g_editor)
				{
					g_editor->draw();
					__IRR_DRIVER()->swapBuffers();
				}
				Sleep(10);
			}
		}
		Sleep(20);
		if (frames > 0 && ++frameCount >= frames) device->closeDevice();
	}

	app->Quit();

	return 0;
}

