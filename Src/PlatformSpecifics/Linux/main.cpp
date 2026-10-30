#include "stdafx.h"
#include "Core/irrlicht.h"
#include "Editor/GS_Editor.h"
#include "Editor/ExternalEditor.h"
#include "Game/Playfield.h"
#include "IO/TouchScreen/TouchPad.h"
#include "CCommonGLDriver.h"
#include <cstdlib>
#include <cstring>
#include <string>

bool g_visualizeWaves = false;

class DesktopInput : public IEventReceiver
{
    bool down[2] = {false, false};
    video::CCommonGLDriver* driver;
public:
    explicit DesktopInput(video::CCommonGLDriver* videoDriver) : driver(videoDriver) {}

    bool OnEvent(const SEvent& event)
    {
        if (g_editor)
        {
            if (event.EventType == EET_KEY_INPUT_EVENT &&
                event.KeyInput.Key == KEY_ESCAPE && !event.KeyInput.PressedDown)
            {
                g_editor->exit();
                driver->setLogicalScreenSize(dimension2d<s32>(OS_SCREEN_W, OS_SCREEN_H));
                return true;
            }
            // Application consumes raw keyboard/mouse events, so deliver them
            // to the editor GUI explicitly. GUI notifications return here too.
            if (event.EventType == EET_KEY_INPUT_EVENT || event.EventType == EET_MOUSE_INPUT_EVENT)
                __IRR_GUI()->postEventFromUser(event);
            g_editor->OnEvent(event);
            // Returning true for GUI focus notifications would veto focus changes.
            return event.EventType == EET_KEY_INPUT_EVENT || event.EventType == EET_MOUSE_INPUT_EVENT;
        }
        if (event.EventType == EET_KEY_INPUT_EVENT && !event.KeyInput.PressedDown)
        {
            if (event.KeyInput.Key == KEY_KEY_E || event.KeyInput.Key == KEY_KEY_R ||
                event.KeyInput.Key == KEY_F9)
            {
                // Unit definitions and level data become available during the splash.
                if (g_unitTypes.empty() || !GS_Editor::GetInstance()->init()) return true;
                if (event.KeyInput.Key == KEY_F9)
                {
                    g_visualizeWaves = !g_visualizeWaves;
                }
                else
                {
                    for (int i = 0; i < 2; ++i)
                    {
                        if (down[i]) CTouchPad::EventTouchUp(i, -1, -1);
                        down[i] = false;
                    }
                    // Use desktop pixels for editor widgets and raw GUI input.
                    driver->setLogicalScreenSize(driver->getPhysicalScreenSize());
                    GS_Editor::GetInstance()->enter();
                }
                return true;
            }
            if (event.KeyInput.Key == KEY_ESCAPE) __IRR_DEVICE()->closeDevice();
            if (event.KeyInput.Key == KEY_F2) os::Timer::setSpeed(os::Timer::getSpeed() * 0.5f);
            if (event.KeyInput.Key == KEY_F3) os::Timer::setSpeed(os::Timer::getSpeed() * 2.0f);
        }
        if (event.EventType != EET_MOUSE_INPUT_EVENT) return false;
        // Touch areas and 3D picking use the original game coordinates.
        const dimension2d<s32>& window = driver->getPhysicalScreenSize();
        const dimension2d<s32>& game = driver->getScreenSize();
        const int x = event.MouseInput.X * game.Width / window.Width;
        const int y = event.MouseInput.Y * game.Height / window.Height;
        switch (event.MouseInput.Event)
        {
        case EMIE_LMOUSE_PRESSED_DOWN: down[0] = true; CTouchPad::EventTouchDown(0,x,y); break;
        case EMIE_RMOUSE_PRESSED_DOWN: down[1] = true; CTouchPad::EventTouchDown(1,x,y); break;
        case EMIE_LMOUSE_LEFT_UP: down[0] = false; CTouchPad::EventTouchUp(0,x,y); break;
        case EMIE_RMOUSE_LEFT_UP: down[1] = false; CTouchPad::EventTouchUp(1,x,y); break;
        case EMIE_MOUSE_MOVED:
            for (int i=0; i<2; ++i) if (down[i]) CTouchPad::EventTouchMove(i,x,y);
            break;
        default: break;
        }
        return false;
    }
};

int main(int argc, char** argv)
{
#ifdef CASTLE_LOCAL_DRI_DIR
    // Mesa/LLVM use RUNPATH for optional libraries. The dynamic loader must see
    // the local dependency directory before it starts, including under GDB.
    const char* previous = getenv("LD_LIBRARY_PATH");
    if (!previous || std::string(previous).find(CASTLE_LOCAL_LIBRARY_DIR) != 0)
    {
        std::string libraries = CASTLE_LOCAL_LIBRARY_DIR;
        if (previous && *previous) libraries += std::string(":") + previous;
        setenv("LD_LIBRARY_PATH", libraries.c_str(), 1);
        execv("/proc/self/exe", argv);
        perror("Restart with local libraries");
        return 1;
    }
    setenv("LIBGL_DRIVERS_PATH", CASTLE_LOCAL_DRI_DIR, 0);
    setenv("LIBGL_ALWAYS_SOFTWARE", "1", 0);
    setenv("__GLX_VENDOR_LIBRARY_NAME", "mesa", 0);
#endif
    const char* assets = CASTLE_ASSET_DIR;
    int frames = 0;
    int scale = 2;
    std::string editorBridge;
    std::string saveDirectory;
    for (int i=1; i<argc; ++i)
    {
        if (!strcmp(argv[i], "--assets") && i+1<argc) assets = argv[++i];
        else if (!strcmp(argv[i], "--frames") && i+1<argc) frames = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--editor-bridge") && i+1<argc) editorBridge = argv[++i];
        else if (!strcmp(argv[i], "--save-dir") && i+1<argc) saveDirectory = argv[++i];
        else if (!strcmp(argv[i], "--scale") && i+1<argc &&
                 argv[i+1][0] >= '1' && argv[i+1][0] <= '4' && argv[i+1][1] == '\0')
            scale = argv[++i][0] - '0';
        else { fprintf(stderr, "Usage: %s [--assets directory] [--frames count] [--scale 1|2|3|4] [--editor-bridge directory] [--save-dir directory]\n", argv[0]); return 2; }
    }
    if (chdir(assets) != 0) { perror(assets); return 1; }
    const char* dataHome = getenv("XDG_DATA_HOME");
    const char* home = getenv("HOME");
    std::string save = dataHome && *dataHome ? dataHome : std::string(home ? home : ".") + "/.local/share";
    // Create parents without invoking a shell; XDG_DATA_HOME may contain spaces.
    save += "/castle-frenzy/";
    if (!saveDirectory.empty()) save = saveDirectory + "/";
    for (size_t i=1; i<save.size(); ++i) if (save[i]=='/') mkdir(save.substr(0,i).c_str(),0700);

    const dimension2d<s32> gameSize(OS_SCREEN_W, OS_SCREEN_H);
    IrrlichtDevice* device = createDevice(video::EDT_OPENGL, gameSize * scale,32,false,false,false,0);
    if (!device) { fprintf(stderr,"Could not create an OpenGL window. Check DISPLAY and graphics drivers.\n"); return 1; }
    video::CCommonGLDriver* driver = static_cast<video::CCommonGLDriver*>(device->getVideoDriver());
    driver->setLogicalScreenSize(gameSize);
    device->setWindowCaption(L"Castle Frenzy - Ubuntu");
    device->setResizeAble(false);
    Application* app = __APP();
    app->setFilePaths("", "./", save.c_str());
    app->InitLinux(device);
    DesktopInput input(driver);
    app->RegisterForIrrlichtEvents(&input);
    int count = 0;
    ExternalEditor externalEditor(editorBridge);
    while (device->run())
    {
        externalEditor.update();
        if (!app->isPaused() && !externalEditor.paused())
        {
            if (g_editor)
            {
                g_editor->update();
                if (g_editor)
                {
                    g_editor->draw();
                    driver->swapBuffers(0);
                }
                else
                {
                    // The editor's Play/Exit buttons also return to the game.
                    driver->setLogicalScreenSize(gameSize);
                }
            }
            else
            {
                if (GS_Editor::GetInstance()->m_icons)
                {
                    // Updating this even when hidden removes old overlay widgets.
                    uiBegin(__IRR_GUI()->getRootGUIElement());
                    CPlayfield* playfield = CPlayfield::GetInstance();
                    if (g_visualizeWaves && canVisualizeWaves(
                            playfield->m_CurrentLevel, playfield->m_CurrentDay))
                    {
                        uiVisualizeWaves(playfield->getWaves().getLast().time + 2.0f,
                            r2i(0, uiParentHeight() - 120, uiParentWidth(), uiParentHeight()));
                    }
                    uiEnd();
                }
                app->Update();
            }
        }
        device->sleep(10);
        if (frames > 0 && ++count >= frames) device->closeDevice();
    }
    app->Quit();
    return 0;
}
