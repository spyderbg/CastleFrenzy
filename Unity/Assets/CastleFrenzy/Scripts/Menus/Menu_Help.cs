// Menus/Menu_Help (enemies/spells help book with 3D unit pages) and Menus/Menu_Ingame_Help.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class HelpPage
    {
        public bool is_active;
        public string string_id = "", string_title_id = "";
        public int page_string_id, page_title_string_id;
        public string node3d_fileName = "";
        public int unit_animation_id = 1, unit_rot_pitch, anim_id = -1;
        public VisualControlMesh meshControl;
        public VisualControlSprite spriteControl;
        public VisualControlText titleControl, contentControl;
        public CUnit unit;
        public bool page_type_3d;
        public int page_num;

        public bool isActive() { return is_active; }
        public void setActive(bool active) { is_active = active; }

        public void Dispose()
        {
            if (meshControl != null) meshControl.SetEnabled(false);
            if (unit != null && unit.getUnitKind() == UK_Bomblin) CPlayfield.GetInstance().ClearProjectiles(true);
            if (unit != null) { unit.Dispose(); unit = null; }
        }

        static int Sid(string sid)
        {
            int id;
            return sid != null && STRING_IDS.BySid.TryGetValue(sid, out id) ? id : -1;
        }

        public bool LoadResurces(ASprite default_sprite, ISceneNode rootNode, int currentBookType)
        {
            page_string_id = Sid(string_id);
            page_title_string_id = Sid(string_title_id);
            if (node3d_fileName.Length == 0 && anim_id == -1) return false;
            if (node3d_fileName.Length != 0)
            {
                unit = new CUnit(UnitParameters.GetUnitParameters(node3d_fileName), rootNode);
                unit.setState(US_None);
                unit.setAnimation(unit_animation_id, true);
                page_type_3d = true;
            }
            else page_type_3d = false;
            const int pageW = GAME_SCREEN_W + 20;
            if (page_type_3d)
            {
                meshControl = new VisualControlMesh();
                meshControl.SetMesh(unit.getSceneNode());
                v3f scale = unit.getSceneNode().getScale();
                meshControl.SetScale(FloatRemap(scale.X, 0.0f, 1.0f, 0.0f, 70.0f));
                meshControl.SetPos(new p2i(-139 + pageW * page_num, -110));
                meshControl.SetEnabled(false);
                bool skeleton = node3d_fileName == "Skeleton" || node3d_fileName == "SkeletonBlue";
                meshControl.SetRotationDeg(new v3f(skeleton ? 12.0f : 0.0f, unit_rot_pitch, 0.0f));
            }
            else
            {
                spriteControl = new VisualControlSprite();
                spriteControl.SetAnimation(default_sprite, anim_id);
                spriteControl.SetPos(new p2i(95 + pageW * page_num, 178));
            }
            int titleX = 0, contentX = 0, contentW = 0;
            const int off = (GAME_SCREEN_W - pageW) / 2;
            if (currentBookType == Menu_Help.MenuHelp_EnemiesBook)
            {
                titleX = GAME_SCREEN_W2 + 80 + off;
                contentX = GAME_SCREEN_W2 + 70 + off;
                contentW = 262;
            }
            else if (currentBookType == Menu_Help.MenuHelp_SpellsBook)
            {
                titleX = GAME_SCREEN_W2 + 96 + off;
                contentX = GAME_SCREEN_W2 + 96 + off;
                contentW = 236;
            }
            titleControl = new VisualControlText();
            titleControl.SetPos(new p2i(titleX + pageW * page_num, 70));
            titleControl.SetText(page_title_string_id);
            titleControl.SetFontColor(0xFF04B1FE);
            titleControl.SetFont("MM_Help");
            titleControl.SetAlign(CFont.HCENTER);
            contentControl = new VisualControlText();
            contentControl.SetPos(new p2i(contentX + pageW * page_num, GAME_SCREEN_H2 + 30));
            contentControl.SetText(page_string_id);
            contentControl.SetFont("MM_Help");
            contentControl.SetRegion(contentW, 200);
            contentControl.SetAlign(CFont.HCENTER | CFont.VCENTER);
            return true;
        }

        public void SetVisible(bool visible)
        {
            if (meshControl != null) { meshControl.SetVisible(visible); meshControl.SetEnabled(visible); }
            if (titleControl != null) titleControl.SetVisible(visible);
            if (contentControl != null) contentControl.SetVisible(visible);
            if (spriteControl != null) spriteControl.SetVisible(visible);
        }
    }

    public class PageManager
    {
        ASprite m_images;
        CCameraSceneNode m_camera, m_oldCamera;
        CLightSceneNode m_light;
        VisualControlPane m_pane, m_enemiesPane, m_spellsPane;
        int m_currentBookType = Menu_Help.MenuHelp_NotSet;
        readonly List<HelpPage> m_spellsBook = new List<HelpPage>(), m_enemiesBook = new List<HelpPage>();
        ISceneNode m_rootNode;
        readonly Dictionary<ISceneNode, bool> m_visibleFlags = new Dictionary<ISceneNode, bool>();

        List<HelpPage> GetBook(int type) { return type == Menu_Help.MenuHelp_EnemiesBook ? m_enemiesBook : m_spellsBook; }
        List<HelpPage> GetCurrentBook() { return GetBook(m_currentBookType); }
        public int GetEnemiesPagesNum() { return m_enemiesBook.Count; }
        public int GetSpellsPagesNum() { return m_spellsBook.Count; }
        public ISceneNode GetRootNode() { return m_rootNode; }

        public void Dispose()
        {
            if (m_light != null) m_light.remove();
            if (m_camera != null) m_camera.remove();
            foreach (HelpPage p in m_spellsBook) p.Dispose();
            foreach (HelpPage p in m_enemiesBook) p.Dispose();
            m_spellsBook.Clear();
            m_enemiesBook.Clear();
            if (m_rootNode != null) m_rootNode.remove();
            m_rootNode = null;
        }

        public bool LoadSprite()
        {
            m_images = new ASprite();
            return m_images.Load("Menu/help_images");
        }

        public void SaveVisibleStates()
        {
            foreach (ISceneNode n in __IRR_SCENE().getRootSceneNode().getChildren()) m_visibleFlags[n] = n.isVisible();
        }

        public void RestoreVisibleStates()
        {
            foreach (ISceneNode n in __IRR_SCENE().getRootSceneNode().getChildren())
            {
                bool v;
                if (m_visibleFlags.TryGetValue(n, out v)) n.setVisible(v);
            }
            m_visibleFlags.Clear();
        }

        public void PrepareScene(bool hideAll)
        {
            foreach (ISceneNode n in __IRR_SCENE().getRootSceneNode().getChildren())
                if (n != m_rootNode) n.setVisible(!hideAll);
            m_rootNode.setVisible(hideAll);
        }

        public void SaveActiveCamera() { m_oldCamera = __IRR_SCENE().getActiveCamera(); }

        public void SetActiveCamera(bool activate)
        {
            if (activate) __IRR_SCENE().setActiveCamera(m_camera);
            else
            {
                __IRR_SCENE().setActiveCamera(m_oldCamera);
                if (m_oldCamera != null) m_oldCamera.updateView();
            }
        }

        public void CreateHelpers()
        {
            m_rootNode = __IRR_SCENE().addEmptySceneNode();
            m_camera = __IRR_SCENE().addCameraSceneNode(m_rootNode);
            m_camera.setProjectionMatrix(matrix4.Identity.buildProjectionMatrixOrtho(480.0f, 320.0f, 1, 150), true);
            m_camera.setTarget(v3f.Zero);
            m_camera.setPosition(new v3f(0, 0, -30));
            m_light = __IRR_SCENE().addLightSceneNode(m_rootNode);
            m_light.getLightData().AmbientColor = new SColorf(1, 1, 1, 1);
            m_light.getLightData().DiffuseColor = new SColorf(0, 0, 0, 0);
            m_rootNode.setVisible(false);
        }

        public bool LoadConfiguration(string config_filename, int type)
        {
            m_currentBookType = type;
            if (type == Menu_Help.MenuHelp_NotSet) return true;
            IXMLReader xml = IXMLReader.FromFile(config_filename);
            if (xml == null) return false;
            HelpPage cpage = null;
            while (xml.read())
            {
                if (xml.getNodeType() == EXML_NODE.EXN_ELEMENT)
                {
                    string objectType = xml.getNodeName();
                    if (objectType == "page") cpage = new HelpPage();
                    else if (cpage == null) continue;
                    else if (objectType == "TEXT_CONTENT_ID") cpage.string_id = xml.getAttributeValue(0);
                    else if (objectType == "TEXT_TITLE_ID") cpage.string_title_id = xml.getAttributeValue(0);
                    else if (objectType == "MODEL_NAME") cpage.node3d_fileName = xml.getAttributeValue(0);
                    else if (objectType == "MODEL_ANIMATION_ID") cpage.unit_animation_id = xml.getAttributeValueAsInt(0);
                    else if (objectType == "MODEL_ROTATION_YAW") cpage.unit_rot_pitch = xml.getAttributeValueAsInt(0);
                    else if (objectType == "ANIM_ID") cpage.anim_id = xml.getAttributeValueAsInt(0);
                }
                else if (xml.getNodeType() == EXML_NODE.EXN_ELEMENT_END && xml.getNodeName() == "page" && cpage != null)
                {
                    GetCurrentBook().Add(cpage);
                    cpage = null;
                }
            }
            List<HelpPage> book = GetCurrentBook();
            for (int i = 0; i < book.Count; i++)
            {
                book[i].page_num = i;
                if (!book[i].LoadResurces(m_images, m_rootNode, m_currentBookType)) return false;
            }
            return true;
        }

        void AddPages(IMenu menu, VisualControlPane pane, List<HelpPage> book)
        {
            VisualControlText dummy = new VisualControlText();
            menu.VisualControlAdd(dummy);
            pane.AddControl(dummy);
            foreach (HelpPage p in book)
            {
                menu.VisualControlAdd(p.titleControl);
                pane.AddControl(p.titleControl);
                menu.VisualControlAdd(p.contentControl);
                pane.AddControl(p.contentControl);
                if (p.meshControl != null) { menu.VisualControlAdd(p.meshControl); pane.AddControl(p.meshControl); }
                else if (p.spriteControl != null) { menu.VisualControlAdd(p.spriteControl); pane.AddControl(p.spriteControl); }
            }
        }

        public void AddControls(IMenu menu, VisualControlPane enemies, VisualControlPane spells)
        {
            AddPages(menu, enemies, m_enemiesBook);
            AddPages(menu, spells, m_spellsBook);
            m_enemiesPane = enemies;
            m_spellsPane = spells;
        }

        public void Draw(ASprite sprite)
        {
            if (m_currentBookType == Menu_Help.MenuHelp_NotSet) return;
            List<HelpPage> book = GetCurrentBook();
            int x1 = GAME_SCREEN_W2 - book.Count * 30 / 2;
            for (int i = 0; i < book.Count; i++)
                sprite.PaintFrame(book[i].isActive() ? MENU_F_MENU_PAGE_ACTIVE : MENU_F_MENU_PAGE_INACTIVE, x1 + i * 30 + 30 / 2, 302);
        }

        public void Update(float dt)
        {
            if (m_pane == null) return;
            List<HelpPage> book = GetCurrentBook();
            for (int i = 0; i < m_pane.GetCols() && i < book.Count; i++) book[i].setActive(i == m_pane.GetCurrentCol());
        }

        public void SetMenuType(int type)
        {
            foreach (HelpPage p in m_enemiesBook) p.SetVisible(false);
            foreach (HelpPage p in m_spellsBook) p.SetVisible(false);
            switch (type)
            {
                case Menu_Help.MenuHelp_EnemiesBook: m_pane = m_enemiesPane; m_currentBookType = type; break;
                case Menu_Help.MenuHelp_SpellsBook: m_pane = m_spellsPane; m_currentBookType = type; break;
                default:
                    m_pane = null;
                    m_currentBookType = Menu_Help.MenuHelp_NotSet;
                    return;
            }
            foreach (HelpPage p in GetCurrentBook()) p.SetVisible(true);
        }
    }

    public class Menu_Help : MenuSimple
    {
        public const int MenuHelp_NotSet = 0, MenuHelp_EnemiesBook = 1, MenuHelp_SpellsBook = 2;
        const int action_back = MB.MENU_HELP_ACTION_BASE, action_back_trans = action_back + 1, action_left = action_back + 2,
            action_left_trans = action_back + 3, action_right = action_back + 4, action_right_trans = action_back + 5, action_vpane = action_back + 6;

        int m_helpBookType = MenuHelp_NotSet;
        bool m_isIngame;
        VisualControlButton m_arrowLeft, m_arrowRight, m_buttonBack;
        PageManager m_pageManager;
        VisualControlPane m_enemiesPane, m_spellsPane, m_currentPane;
        CTouchElement m_TW_pages;
        bool m_firstDraw = true;

        public Menu_Help(IMenu parent) : base(parent) { }

        public void SetHelpBookType(int type) { m_helpBookType = type; }
        public void SetIsMenuIngame(bool is_ingame) { m_isIngame = is_ingame; }

        public override void Initialize()
        {
            LoadResources();
            LoadConfigFile();
            SetMenuName(STRING_IDS.MENU_HELP_TITLE);
            m_arrowLeft = new VisualControlButton(action_left, -1, MB.k_back_button_sfx);
            m_arrowLeft.SetAnimations(m_sprite, MENU_A_MENU_ARROW_LEFT_NORMAL, MENU_A_MENU_ARROW_LEFT_NORMAL, MENU_A_MENU_ARROW_LEFT_NORMAL);
            m_arrowLeft.SetArea(17, 169, m_sprite.GetFrameWidth(MENU_F_MENU_ARROW_LEFT), m_sprite.GetFrameHeight(MENU_F_MENU_ARROW_LEFT));
            m_arrowLeft.SetEnabled(false);
            VisualControlAdd(m_arrowLeft);
            m_arrowRight = new VisualControlButton(action_right, -1, MB.k_back_button_sfx);
            m_arrowRight.SetAnimations(m_sprite, MENU_A_MENU_ARROW_RIGHT_NORMAL, MENU_A_MENU_ARROW_RIGHT_NORMAL, MENU_A_MENU_ARROW_RIGHT_NORMAL);
            m_arrowRight.SetArea(463, 170, m_sprite.GetFrameWidth(MENU_F_MENU_AROW_RIGHT), m_sprite.GetFrameHeight(MENU_F_MENU_AROW_RIGHT));
            m_arrowRight.SetEnabled(false);
            VisualControlAdd(m_arrowRight);
            m_buttonBack = MB.AddBackButton(this, m_sprite, action_back);
            m_TW_pages = new CTouchElement();
            m_TW_pages.SetScreenCoordinates(0, GAME_SCREEN_H - 35, GAME_SCREEN_W, 35);
            m_TW_pages.SetEnabled(false);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
            m_pageManager.SetMenuType(MenuHelp_NotSet);
        }

        public override void GainFocus()
        {
            base.GainFocus();
            GetParent().VisualControls_SetEnableTE(false);
            m_pageManager.SaveVisibleStates();
            m_pageManager.SaveActiveCamera();
            m_TW_pages.SetEnabled(true);
            int title = STRING_IDS.MENU_INFO_HELP_ENEMIES;
            if (m_helpBookType == MenuHelp_SpellsBook)
            {
                m_enemiesPane.SetEnabled(false);
                m_spellsPane.SetEnabled(true);
                m_currentPane = m_spellsPane;
                title = STRING_IDS.MENU_INFO_HELP_SPELLS;
            }
            else
            {
                m_helpBookType = MenuHelp_EnemiesBook;
                m_spellsPane.SetEnabled(false);
                m_enemiesPane.SetEnabled(true);
                m_currentPane = m_enemiesPane;
            }
            m_pageManager.SetMenuType(m_helpBookType);
            if (m_isIngame) ((Menu_Ingame)GetParent()).GetTitleTextControl().SetText(title);
            else SetMenuName(title);
            m_currentPane.ReturnToStartPos();
            m_firstDraw = true;
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            m_pageManager.PrepareScene(false);
            GetParent().VisualControls_SetEnableTE(true);
            m_pageManager.RestoreVisibleStates();
            m_pageManager.SetActiveCamera(false);
            m_TW_pages.SetEnabled(false);
            m_helpBookType = MenuHelp_NotSet;
            m_pageManager.SetMenuType(MenuHelp_NotSet);
            m_firstDraw = true;
        }

        void LoadConfigFile()
        {
            m_pageManager = new PageManager();
            m_enemiesPane = new VisualControlPane(action_vpane);
            VisualControlAdd(m_enemiesPane);
            m_spellsPane = new VisualControlPane(action_vpane);
            VisualControlAdd(m_spellsPane);
            if (!m_pageManager.LoadSprite()) { Logs.error("Menu_Help: could not load sprite"); return; }
            m_pageManager.CreateHelpers();
            if (!m_pageManager.LoadConfiguration("config/spells_help.xml", MenuHelp_SpellsBook)) Logs.error("Menu_Help: bad config/spells_help.xml");
            if (!m_pageManager.LoadConfiguration("config/enemies_help.xml", MenuHelp_EnemiesBook)) Logs.error("Menu_Help: bad config/enemies_help.xml");
            m_pageManager.LoadConfiguration("", MenuHelp_NotSet);
            r2i screenArea = new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
            int enemiesCount = m_pageManager.GetEnemiesPagesNum(), spellsCount = m_pageManager.GetSpellsPagesNum();
            const int type = VisualControlPane.CPT_AllowContMove | VisualControlPane.CPT_Horizontal | VisualControlPane.CPT_AlignControls;
            m_enemiesPane.SetArea(screenArea);
            m_enemiesPane.SetScrollSize(new d2i((GAME_SCREEN_W + 20) * enemiesCount, GAME_SCREEN_H));
            m_enemiesPane.SetType(type);
            m_enemiesPane.SetCols(enemiesCount);
            m_enemiesPane.SetEnabled(false);
            m_spellsPane.SetArea(screenArea);
            m_spellsPane.SetScrollSize(new d2i((GAME_SCREEN_W + 20) * spellsCount, GAME_SCREEN_H));
            m_spellsPane.SetType(type);
            m_spellsPane.SetCols(spellsCount);
            m_spellsPane.SetEnabled(false);
            m_pageManager.AddControls(this, m_enemiesPane, m_spellsPane);
        }

        public override void Destroy()
        {
            if (m_music_id != -1) __MGR_SOUNDS().stop(m_music_id);
            if (m_pageManager != null) { m_pageManager.Dispose(); m_pageManager = null; }
            if (m_TW_pages != null) { m_TW_pages.Dispose(); m_TW_pages = null; }
            VisualControls_Free();
            m_isInitialized = false;
            SubMenus_Destroy();
            FreeResources();
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            m_pageManager.SetActiveCamera(true);
            Gfx.draw2DRectangle(new SColor(150, 0, 0, 0), new r2i(0, 60, 480, 285));
            m_pageManager.Draw(m_sprite);
            VisualControls_Draw();
            m_pageManager.PrepareScene(true);
            Gfx.endScene2D();
            if (!m_firstDraw)
            {
                Gfx.clearZBuffer();
                __IRR_SCENE().drawAll();
            }
            Gfx.beginScene2D();
            if (m_isIngame) m_pageManager.SetActiveCamera(false);
            m_firstDraw = false;
        }

        public void ResetGraphics()
        {
            m_pageManager.SetActiveCamera(false);
            m_pageManager.PrepareScene(false);
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized || m_currentPane == null) return;
            m_arrowLeft.SetVisible(m_currentPane.GetCurrentCol() > 0);
            m_arrowRight.SetVisible(m_currentPane.GetCurrentCol() < m_currentPane.GetCols() - 1);
            if (m_TW_pages.IsActive())
            {
                int x, y;
                m_TW_pages.GetTouchPos_Current(out x, out y);
                int pages = m_currentPane.GetCols();
                int x1 = GAME_SCREEN_W2 - pages * 30 / 2, x2 = GAME_SCREEN_W2 + pages * 30 / 2;
                if (x >= x1 && x <= x2) m_currentPane.GoToCol((x - x1) / 30);
            }
            base.Update(dt);
            if (IsOnFocus()) m_pageManager.Update(dt);
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            if (action_id == action_back) Transition_Start(transition_hide, action_back_trans);
            else if (action_id == action_back_trans) Go(m_isIngame ? "Menu_Ingame_Help" : "Menu_Info");
        }
    }

    public class Menu_Ingame_Help : MenuSimple
    {
        const int action_back = MB.MENU_INGAME_HELP_ACTION_BASE, action_back_trans = action_back + 1, action_enemies_book = action_back + 2,
            action_enemies_book_trans = action_back + 3, action_spell_book = action_back + 4, action_spell_book_trans = action_back + 5;

        bool m_backClicked;

        public Menu_Ingame_Help(IMenu parent) : base(parent) { }

        public bool isBack() { return m_backClicked; }

        public override void Initialize()
        {
            LoadResources();
            VisualControlButton e = MB.AddBaseButton(this, m_sprite, action_enemies_book, GAME_SCREEN_W2, GAME_SCREEN_H2 - 30, STRING_IDS.MENU_INFO_HELP_ENEMIES);
            VisualControlButton s = MB.AddBaseButton(this, m_sprite, action_spell_book, GAME_SCREEN_W2, GAME_SCREEN_H2 + 30, STRING_IDS.MENU_INFO_HELP_SPELLS);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void GainFocus()
        {
            base.GainFocus();
            GetParent().VisualControls_SetEnableTE(false);
            m_backClicked = false;
            ((Menu_Ingame)GetParent()).GetTitleTextControl().SetText(STRING_IDS.MENU_INFO_HELP_BOOK);
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            GetParent().VisualControls_SetEnableTE(true);
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            Menu_Ingame parent = (Menu_Ingame)GetParent();
            switch (action_id)
            {
                case action_back:
                    GetParent().Transition_Start(transition_show);
                    m_backClicked = true;
                    break;
                case action_back_trans: m_backClicked = true; break;
                case action_enemies_book: Transition_Start(transition_hide, action_enemies_book_trans); break;
                case action_enemies_book_trans:
                    parent.GetHelpMenu().SetHelpBookType(Menu_Help.MenuHelp_EnemiesBook);
                    parent.GetHelpMenu().SetIsMenuIngame(true);
                    GetParent().SubMenu_Change("Menu_Help");
                    break;
                case action_spell_book: Transition_Start(transition_hide, action_spell_book_trans); break;
                case action_spell_book_trans:
                    parent.GetHelpMenu().SetHelpBookType(Menu_Help.MenuHelp_SpellsBook);
                    parent.GetHelpMenu().SetIsMenuIngame(true);
                    GetParent().SubMenu_Change("Menu_Help");
                    break;
            }
        }
    }
}
