// Menus/Menu_Shop: the weapon carousel between days (3D weapons on a rotating ring, upgrade slider, prices).
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class Menu_Shop : MenuSimple
    {
        const int action_left = MB.MENU_SHOP_ACTION_BASE, action_right = action_left + 1, action_level = action_left + 2,
            action_buy = action_left + 3, action_exit = action_left + 4;

        const float k_weapon_scale = 25.0f;
        const float k_weapon_rot = PI / 8.0f;
        static readonly v3f k_weapon_camera_pos = new v3f(0, 2, -7).setLength(325);
        static readonly v3f k_weapon_pos_center = new v3f(120, 0, -160);
        const float k_weapon_pos_radius = 95;
        const float k_weapon_pos_angle = PI;
        const int k_desc_x = 352, k_desc_y = 152, k_desc_w = 228;
        const int k_desc_title_y = k_desc_y - 90 + 5, k_desc_text_x = k_desc_x - 2, k_desc_text_y = k_desc_y - 28 + 15, k_desc_text_w = k_desc_w - 2 * 18;
        const int k_desc_slider_y = k_desc_y + 67, k_desc_slider_w = 170, k_desc_slider_h = 52;
        const int k_desc_bottom_y = k_desc_y + 98, k_desc_level_x = k_desc_x - 90;
        const int k_desc_gold_icon_x = k_desc_x + 39, k_desc_gold_value_x = k_desc_x + 50, k_desc_souls_icon_x = k_desc_x - 15, k_desc_souls_value_x = k_desc_x - 3;
        const int k_bottom_y = 295, k_buy_w = 117, k_buy_h = 46;
        const int k_soul_box_x = 86, k_soul_icon_x = k_soul_box_x - 62, k_soul_value_x = k_soul_box_x + 34;
        const int k_money_box_x = 226, k_money_icon_x = k_money_box_x - 62, k_money_value_x = k_money_box_x + 34;

        int m_selected, m_level;
        VisualControlButton m_btn_buy, m_btn_left, m_btn_right;
        VisualControlSlider m_slider_level;
        VisualControlText m_titleText;
        CTouchElement m_TE_weapons;
        float m_angle_current, m_angle_target;
        bool m_angle_skip, m_exit;
        CCameraSceneNode m_camera;
        CLightSceneNode m_light;
        ISceneNode m_floor, m_hydraPool;
        readonly CWeaponObject[] m_weapons = new CWeaponObject[WT_Count];
        int m_weaponCount;
        readonly float[] m_highlight = new float[WT_Count];
        int m_nextCutscene;
        ASprite m_shopSprite;
        readonly Menu_Confirmation m_menuConfirmation;

        public Menu_Shop(IMenu parent) : base(parent)
        {
            m_menuConfirmation = new Menu_Confirmation(this);
            SubMenu_Register("Menu_Confirmation", m_menuConfirmation);
        }

        protected override void LoadResources()
        {
            m_sprite = new ASprite();
            m_sprite.Load("Menu/menu");
            m_shopSprite = new ASprite();
            m_shopSprite.Load("Menu/shop");
            m_floor = LoadColladaScene("Scenes/MenuShop/shop_floor.bdae");
            cleanNode(m_floor);
            __IRR_SCENE().getRootSceneNode().addChild(m_floor);
            m_camera = __IRR_SCENE().addCameraSceneNode();
            m_light = __IRR_SCENE().addLightSceneNode();
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            m_hydraPool = null;
            m_weaponCount = 0;
            for (int i = 0; i < WT_Count; i++)
            {
                m_weapons[i] = null;
                if (!stats.WeaponAvailable[i]) continue;
                switch (i)
                {
                    case WT_Gate: m_weapons[m_weaponCount++] = new CCastleGate(); break;
                    case WT_Hydra:
                        m_weapons[m_weaponCount++] = new CHydra();
                        m_hydraPool = LoadColladaScene("Scenes/Hydra/hydra_pool.bdae");
                        cleanNode(m_hydraPool);
                        __IRR_SCENE().getRootSceneNode().addChild(m_hydraPool);
                        Timeline(m_hydraPool).setClip("water");
                        Timeline(m_hydraPool).setLoop(true);
                        break;
                    case WT_Fireball: m_weapons[m_weaponCount++] = new CFireball(); break;
                    case WT_Iceball: m_weapons[m_weaponCount++] = new CIceball(); break;
                    case WT_Griffin: m_weapons[m_weaponCount++] = new CGriffin(); break;
                    case WT_Spell: if (stats.SpellAvailable != ST_None) m_weapons[m_weaponCount++] = new CSpell(); break;
                }
            }
            for (int i = 0; i < m_weaponCount; i++) m_highlight[i] = 0.0f;
        }

        protected override void FreeResources()
        {
            if (m_hydraPool != null) { m_hydraPool.remove(); m_hydraPool = null; }
            for (int i = 0; i < m_weaponCount; i++) { if (m_weapons[i] != null) m_weapons[i].Dispose(); m_weapons[i] = null; }
            m_weaponCount = 0;
            if (m_light != null) m_light.remove();
            if (m_camera != null) m_camera.remove();
            if (m_floor != null) m_floor.remove();
            m_light = null; m_camera = null; m_floor = null;
            m_sprite = null;
            m_shopSprite = null;
        }

        public override void Initialize()
        {
            LoadResources();
            CPlayfield playfield = CPlayfield.GetInstance();
            if (playfield.IsLevelLoaded())
            {
                playfield.hideForeground();
                playfield.hideBackground();
                playfield.getTouch().SetEnabled(false);
            }
            m_selected = 0;
            m_TE_weapons = new CTouchElement(CTouchElement.Type_Drag, 60);
            m_TE_weapons.SetScreenCoordinates(20, 50, 200, 200);
            m_TE_weapons.SetEnabled(true);
            m_angle_current = m_angle_target = 0.0f;
            m_angle_skip = false;
            m_btn_left = new VisualControlButton(action_left, -1, -1);
            m_btn_left.SetArea(20 + 10, 50 + 200 / 2, 60, 60);
            m_btn_left.SetAnimations(m_shopSprite, MENU_A_SHOP_LEFT_NORMAL, MENU_A_SHOP_LEFT_PRESSED, MENU_A_SHOP_LEFT_BLINK);
            VisualControlAdd(m_btn_left);
            m_btn_right = new VisualControlButton(action_right, -1, -1);
            m_btn_right.SetArea(20 + 200 - 10, 50 + 200 / 2, 60, 60);
            m_btn_right.SetAnimations(m_shopSprite, MENU_A_SHOP_RIGHT_NORMAL, MENU_A_SHOP_RIGHT_PRESSED, MENU_A_SHOP_RIGHT_BLINK);
            VisualControlAdd(m_btn_right);
            m_btn_buy = new VisualControlButton(action_buy, -1, SFX_MENU_BUY);
            m_btn_buy.SetArea(k_desc_x, k_bottom_y, k_buy_w, k_buy_h);
            m_btn_buy.SetText1(_TXT(STRING_IDS.COMMAND_BUY), "MM_Normal", "MM_Pressed", new vcRect(0, 0, 0, 0));
            m_btn_buy.SetAnimations(m_sprite, MENU_A_MENU_BUY_BUTTON_NORMAL, MENU_A_MENU_BUY_BUTTON_PRESSED, MENU_A_MENU_BUY_BUTTON_BLINK, MENU_A_MENU_BUY_BUTTON_DISABLED);
            VisualControlAdd(m_btn_buy);
            int sw = m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON_SMALL), sh = m_sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON_SMALL);
            VisualControlButton exit = new VisualControlButton(action_exit, -1, SFX_MENU_SELECT);
            exit.SetAnimations(m_sprite, MENU_A_MENU_SHOP_BUTTON_BACK_NORMAL, MENU_A_MENU_SHOP_BUTTON_BACK_PRESSED, MENU_A_MENU_SHOP_BUTTON_BACK_BLINK);
            exit.SetArea((sw >> 1) + 5, 25, sw, sh);
            exit.SetText1(STRING_IDS.MENU_SHOP_BACK_TEXT, "MM_Normal", "MM_Pressed", new vcRect(0, 0, 0, 0));
            VisualControlAdd(exit);
            m_exit = false;
            m_slider_level = new VisualControlSlider(action_level);
            m_slider_level.SetArea(k_desc_x, k_desc_slider_y, k_desc_slider_w, k_desc_slider_h);
            m_slider_level.SetFrames(m_shopSprite, MENU_F_SHOP_SLIDER_BG_00, MENU_F_SHOP_SLIDER_FILL, MENU_F_SHOP_SLIDER_BTN_NORMAL, MENU_F_SHOP_SLIDER_BTN_GLOW);
            m_slider_level.SetPercent(50);
            VisualControlAdd(m_slider_level);
            m_titleText = new VisualControlText();
            m_titleText.SetText(STRING_IDS.MENU_SHOP_TITLE);
            m_titleText.SetPos(new p2i(GAME_SCREEN_W - (m_sprite.GetFrameWidth(MENU_F_MENU_SHOP_TITLE) >> 1), 25));
            m_titleText.SetFont("MM_Normal");
            m_titleText.SetAlign(CFont.HVCENTER);
            VisualControlAdd(m_titleText);
            const float c = 1.0f / 255.0f * 0x55;
            m_light.getLightData().AmbientColor = new SColorf(c, c, c, c);
            m_light.getLightData().DiffuseColor = new SColorf(0, 0, 0, 0);
            m_light.getLightData().SpecularColor = new SColorf(0, 0, 0, 0);
            m_camera.setProjectionMatrix(matrix4.Identity.buildProjectionMatrixOrtho(480.0f, 320.0f, 20, 1000), true);
            m_camera.setTarget(v3f.Zero);
            m_camera.setPosition(k_weapon_camera_pos);
            m_floor.setScale(new v3f(-1, 1, -1) * 5.0f);
            ISceneNode plane = m_floor.getSceneNodeFromName("Plane01");
            if (plane != null && plane.getChildren().Count > 0 && plane.getChildren()[0].getMaterialCount() > 0)
                plane.getChildren()[0].getMaterial(0).MaterialType = E_MATERIAL_TYPE.EMT_SOLID;
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            for (int i = 0; i < m_weaponCount; i++)
            {
                int type = m_weapons[i].getType();
                int level = type == WT_Spell ? stats.SpellAvailable : CLAMP(stats.WeaponLevel[type], WL_First, WL_Last);
                m_weapons[i].setLevel(level);
                m_weapons[i].reset();
                m_weapons[i].setShopState();
            }
            SetStartingWeapon();
            m_nextCutscene = CPlayfield.CS_Invalid;
            SubMenus_Initialize();
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            if (m_music_id != -1) __MGR_SOUNDS().stop(m_music_id);
            VisualControls_Free();
            if (m_nextCutscene != CPlayfield.CS_Invalid) CPlayfield.GetInstance().SetNextCutscene(m_nextCutscene);
            CPlayfield.GetInstance().WriteProfileStats();
            m_isInitialized = false;
            FreeResources();
            if (m_TE_weapons != null) { m_TE_weapons.Dispose(); m_TE_weapons = null; }
            SubMenus_Destroy();
        }

        public override void GainFocus()
        {
            base.GainFocus();
            SelectWeapon(m_selected);
            m_TE_weapons.SetEnabled(true);
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            m_TE_weapons.SetEnabled(false);
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            __IRR_SCENE().setActiveCamera(m_camera);
            Gfx.endScene2D();
            m_floor.setVisible(true);
            for (int i = 0; i < m_weaponCount; i++) m_weapons[i].setVisible(false);
            __IRR_SCENE().drawAll();
            Gfx.beginScene2D();
            m_shopSprite.PaintFrame(MENU_F_SHOP_BG_SHOP, 0, 0, 255);
            Gfx.endScene2D();
            m_floor.setVisible(false);
            for (int i = 0; i < m_weaponCount; i++) m_weapons[i].setVisible(true);
            Gfx.clearZBuffer();
            __IRR_SCENE().drawAll();
            Gfx.beginScene2D();
            DrawWeaponDescription();
            m_sprite.PaintFrame(MENU_F_MENU_SHOP_TITLE, GAME_SCREEN_W - (m_sprite.GetFrameWidth(MENU_F_MENU_SHOP_TITLE) >> 1), 25);
            DrawAvailableMoney();
            VisualControls_Draw();
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            base.Update(dt);
            float dt_sec = dt / 1000;
            UpdateScrollInput(dt_sec);
            UpdateSliderInput();
            UpdateHighlight(dt_sec);
            for (int i = 0; i < m_weaponCount; i++) m_weapons[i].update(dt_sec);
            UpdateMenuMusic();
            if (Transition_IsGoing()) return;
            if (m_menuConfirmation.IsOnFocus() && m_menuConfirmation.GetDialogResult() != Menu_Confirmation.RESULT_NONE)
            {
                int result = m_menuConfirmation.GetDialogResult();
                SubMenu_Remove();
                if (result == Menu_Confirmation.RESULT_YES)
                {
                    m_exit = true;
                    CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
                    int i = 0;
                    while (i < WT_Count - 1 && stats.WeaponLevel[i] == WL_Count - 1) i++;
                    if (i == WT_Count - 1) CAchievements.GetInstance().setAchieved("Lock & Load");
                }
                GainFocus();
            }
            if (m_exit)
            {
                m_exit = false;
                __MGR_STATES().switchState(new GSLevel());
            }
            CAchievements.GetInstance().addValue("Addicted", (uint)dt);
        }

        void DrawWeaponDescription()
        {
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            int weapon = m_weapons[m_selected].getType();
            SUpgradeInfo info = SUpgradeInfo.GetWeaponInfo(weapon, m_level);
            int gold = info.getPriceGold(), souls = info.getPriceSouls();
            m_shopSprite.PaintFrame(MENU_F_SHOP_WINDOW, k_desc_x, k_desc_y, 255);
            CFont titleFont = __FONT("MS_Title");
            float oldScaleX = titleFont.GetScaleX(), oldScaleY = titleFont.GetScaleY();
            titleFont.SetScale(0.8f);
            titleFont.DrawString(_TXT_INT(info.Name), k_desc_x, k_desc_title_y, CFont.HVCENTER);
            titleFont.SetScale(oldScaleX, oldScaleY);
            __FONT("MS_Text").DrawWrappedText(_TXT_INT(info.Description), k_desc_text_x, k_desc_text_y, k_desc_text_w, CFont.HVCENTER);
            if (weapon != WT_Spell)
                __FONT("MS_SmallNormal").DrawString(FormatString("%s %d", _TXT(STRING_IDS.MENU_LEVEL), m_level + 1), k_desc_level_x, k_desc_bottom_y, CFont.VCENTER);
            if (IsWeaponBought(m_selected, m_level))
                __FONT("MS_SmallNormal").DrawString(_TXT(STRING_IDS.MENU_SOLD), k_desc_gold_value_x, k_desc_bottom_y, CFont.VCENTER | CFont.HCENTER);
            else
            {
                m_shopSprite.PaintFrame(MENU_F_SHOP_GOLD, k_desc_gold_icon_x, k_desc_bottom_y - 1);
                __FONT(gold <= stats.Gold ? "MS_SmallNormal" : "MS_SmallRed").DrawString(FormatNumber(gold), k_desc_gold_value_x, k_desc_bottom_y, CFont.VCENTER);
                m_shopSprite.PaintFrame(MENU_F_SHOP_SOULS, k_desc_souls_icon_x, k_desc_bottom_y - 1);
                __FONT(souls <= stats.Souls ? "MS_SmallNormal" : "MS_SmallRed").DrawString(FormatNumber(souls), k_desc_souls_value_x, k_desc_bottom_y, CFont.VCENTER);
            }
        }

        void DrawAvailableMoney()
        {
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            CFont font = __FONT("MS_SmallGold");
            m_shopSprite.PaintFrame(MENU_F_SHOP_MONEY_SOUL_BAR, k_soul_box_x, k_bottom_y);
            m_shopSprite.PaintFrame(MENU_F_SHOP_SOULS, k_soul_icon_x, k_bottom_y);
            font.DrawString(FormatNumber(stats.Souls), k_soul_value_x, k_bottom_y, CFont.VCENTER | CFont.RIGHT);
            m_shopSprite.PaintFrame(MENU_F_SHOP_MONEY_SOUL_BAR, k_money_box_x, k_bottom_y);
            m_shopSprite.PaintFrame(MENU_F_SHOP_GOLD, k_money_icon_x, k_bottom_y);
            font.DrawString(FormatNumber(stats.Gold), k_money_value_x, k_bottom_y, CFont.VCENTER | CFont.RIGHT);
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case button_action_lose_focus:
                    if (vc.GetActionId() == action_left || vc.GetActionId() == action_right) vc.GetTouchElement().ReassignTouchTo(m_TE_weapons);
                    break;
                case action_left: NextWeapon(); break;
                case action_right: PrevWeapon(); break;
                case action_buy:
                    BuyWeapon(m_selected, m_level);
                    SelectWeapon(m_selected);
                    break;
                case action_exit:
                    m_menuConfirmation.SetConfirmationText(STRING_IDS.MENU_EXIT_SHOP_CONFIRMATION);
                    LoseFocus();
                    SubMenu_Change("Menu_Confirmation");
                    break;
            }
        }

        void SelectWeapon(int item)
        {
            m_selected = item;
            int weapon = m_weapons[item].getType();
            SelectLevel(m_weapons[item].getLevel());
            if (weapon == WT_Spell)
            {
                m_slider_level.SetVisible(false);
                m_slider_level.SetEnabled(false);
            }
            else
            {
                int level = CPlayfield.GetInstance().GetLevelStats().WeaponLevel[weapon];
                m_slider_level.ChangeFrame(VisualControlSlider.slider_frame_empty, MENU_F_SHOP_SLIDER_BG_00 + level + 1);
                m_slider_level.SetVisible(true);
                m_slider_level.SetEnabled(true);
            }
        }

        void SelectLevel(int level)
        {
            m_level = level;
            m_weapons[m_selected].setLevel(m_level);
            int frame = MENU_F_SHOP_SLIDER_BTN_NORMAL, frame_glow = MENU_F_SHOP_SLIDER_BTN_GLOW;
            if (IsWeaponBought(m_selected, m_level)) m_btn_buy.SetEnabled(false);
            else if (CanBuyWeapon(m_selected, m_level))
            {
                m_btn_buy.SetEnabled(true);
                m_btn_buy.SetSelected(false);
            }
            else
            {
                m_btn_buy.SetEnabled(false);
                frame = frame_glow = MENU_F_SHOP_SLIDER_BTN_DISABLED;
            }
            m_slider_level.ChangeFrame(VisualControlSlider.slider_frame_btn, frame);
            m_slider_level.ChangeFrame(VisualControlSlider.slider_frame_btn_glow, frame_glow);
        }

        void NextWeapon()
        {
            AdjustTargetAngle(+2.0f * PI / m_weaponCount);
            __MGR_SOUNDS().play(SFX_MENU_SLIDER_MOVE);
        }

        void PrevWeapon()
        {
            AdjustTargetAngle(-2.0f * PI / m_weaponCount);
            __MGR_SOUNDS().play(SFX_MENU_SLIDER_MOVE);
        }

        void RotateWeapons(float angle)
        {
            for (int i = 0; i < m_weaponCount; i++)
            {
                float rot = angle - WeaponToAngle(i);
                float scale_ratio = (cosf(rot) + 1) / 2.0f;
                rot += k_weapon_pos_angle;
                v3f pos = new v3f(0, 0, k_weapon_pos_radius);
                pos.rotateXZBy(rot * 360.0f / (2.0f * PI));
                pos += k_weapon_pos_center;
                v3f rotation = new v3f(0, k_weapon_rot, 0);
                const float scale_min = 0.8f, scale_max = 1.0f;
                float scale = (scale_min + (scale_max - scale_min) * scale_ratio) * k_weapon_scale;
                CWeaponObject weapon = m_weapons[i];
                switch (weapon.getType())
                {
                    case WT_Gate:
                        weapon.setPosition(pos);
                        weapon.setScale(scale);
                        weapon.getSceneNode().setRotation(new quaternion(-rotation + new v3f(0, PI, 0)));
                        break;
                    case WT_Hydra:
                        m_hydraPool.setPosition(pos);
                        m_hydraPool.setScale(new v3f(1, 1, 1) * scale);
                        m_hydraPool.setRotation(new quaternion(0, PI, 0));
                        pos += new v3f(0, -25, 0);
                        weapon.setPosition(pos);
                        weapon.setScale(scale);
                        weapon.getSceneNode().setRotation(new quaternion(rotation + new v3f(0, PI, 0)));
                        break;
                    case WT_Fireball:
                    {
                        CFireball f = (CFireball)weapon;
                        f.setPosition(pos + new v3f(0, 63, 0));
                        f.setScale(scale);
                        f.getSceneNode().setRotation(new quaternion(rotation));
                        f.getPostament().setPosition(pos);
                        f.getPostament().setScale(new v3f(1, 1, 1) * scale);
                        f.getPostament().setRotation(new quaternion(rotation + new v3f(0, PI, 0)));
                        break;
                    }
                    case WT_Iceball:
                    {
                        CIceball ic = (CIceball)weapon;
                        ic.setPosition(pos + new v3f(0, 67, 0));
                        ic.setScale(scale);
                        ic.getSceneNode().setRotation(new quaternion(rotation));
                        ic.getPostament().setPosition(pos);
                        ic.getPostament().setScale(new v3f(1, 1, 1) * scale);
                        ic.getPostament().setRotation(new quaternion(rotation + new v3f(0, PI, 0)));
                        break;
                    }
                    case WT_Griffin:
                        weapon.setPosition(pos);
                        weapon.setScale(scale);
                        weapon.getSceneNode().setRotation(new quaternion(rotation));
                        break;
                    case WT_Spell:
                        weapon.getSceneNode().setPosition(pos + new v3f(0, 60, 0));
                        weapon.getSceneNode().setScale(new v3f(1, 1, 1) * scale * 1.6f);
                        weapon.getSceneNode().setRotation(new quaternion(rotation + new v3f(0, PI, 0)));
                        break;
                }
            }
        }

        bool IsWeaponBought(int item, int level)
        {
            int weapon = m_weapons[item].getType();
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            if (CGameConfig.AllowDowngrades) return level == stats.WeaponLevel[weapon];
            if (weapon == WT_Spell) return stats.WeaponLevel[weapon] == stats.SpellAvailable;
            return stats.WeaponLevel[weapon] >= level;
        }

        bool CanBuyWeapon(int item, int level)
        {
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            SUpgradeInfo info = SUpgradeInfo.GetWeaponInfo(m_weapons[item].getType(), level);
            return stats.Gold >= info.getPriceGold() && stats.Souls >= info.getPriceSouls();
        }

        void BuyWeapon(int item, int level)
        {
            int weapon = m_weapons[item].getType();
            CLevelStats stats = CPlayfield.GetInstance().GetLevelStats();
            SUpgradeInfo info = SUpgradeInfo.GetWeaponInfo(weapon, level);
            int oldLevel = stats.WeaponLevel[weapon];
            if (weapon != WT_Spell && level >= stats.WeaponLevel[weapon] + 2) CAchievements.GetInstance().setAchieved("Picky Customer");
            stats.WeaponLevel[weapon] = level;
            stats.Gold -= info.getPriceGold();
            stats.Souls -= info.getPriceSouls();
            if (oldLevel == WL_None && weapon == WT_Griffin)
                m_nextCutscene = stats.WeaponLevel[WT_Hydra] == WL_None ? CPlayfield.CS_Griffin : CPlayfield.CS_HydraGriffin;
            else if (weapon == WT_Hydra && level == WL_1)
                m_nextCutscene = stats.WeaponLevel[WT_Griffin] == WL_None ? CPlayfield.CS_Hydra : CPlayfield.CS_HydraGriffin;
            else if (weapon == WT_Hydra && level == WL_3)
                m_nextCutscene = CPlayfield.CS_Hydra3Heads;
        }

        int AngleToWeapon(float angle)
        {
            float snap_angle = 2.0f * PI / m_weaponCount;
            angle += snap_angle / 2;
            while (angle < 0.0f) angle += 2.0f * PI;
            while (angle >= 2.0f * PI) angle -= 2.0f * PI;
            return CLAMP((int)(angle / snap_angle), 0, m_weaponCount - 1);
        }

        float WeaponToAngle(int weapon) { return 2.0f * PI * weapon / m_weaponCount; }

        void AdjustTargetAngle(float offset)
        {
            m_angle_target += offset;
            while (m_angle_target >= 2 * PI) { m_angle_target -= PI * 2; m_angle_current -= PI * 2; }
            while (m_angle_target < 0) { m_angle_target += PI * 2; m_angle_current += PI * 2; }
        }

        void SetStartingWeapon()
        {
            int selected = 0;
            for (int i = 0; i < m_weaponCount; i++)
            {
                int type = m_weapons[i].getType();
                int level = m_weapons[i].getLevel();
                if (!IsWeaponBought(i, level) && CanBuyWeapon(i, level)) { selected = i; break; }
                if (type == WT_Spell) continue;
                int levelNext = CLAMP(level + 1, WL_First, WL_Last);
                if (!IsWeaponBought(i, levelNext) && CanBuyWeapon(i, levelNext))
                {
                    m_weapons[i].setLevel(levelNext);
                    selected = i;
                    break;
                }
            }
            AdjustTargetAngle(WeaponToAngle(selected));
            m_angle_current = m_angle_target;
            SelectWeapon(selected);
        }

        void UpdateScrollInput(float time)
        {
            if (m_TE_weapons.IsNowActivated()) m_TE_weapons.DisableAllOtherElements(true);
            if (m_TE_weapons.IsNowReleased())
            {
                m_TE_weapons.DisableAllOtherElements(false);
                m_TE_weapons.ResetInput();
            }
            if (m_TE_weapons.IsActive())
            {
                int dx, dy;
                m_TE_weapons.GetTouchPos_DT(out dx, out dy);
                float rotate = 2.0f * PI * -dx / 360;
                if (!m_angle_skip)
                {
                    float speed = time > 0 ? fabsf(rotate / time) : 0;
                    if (speed >= 2.0f * PI * 1.5f)
                    {
                        m_angle_skip = true;
                        if (rotate > 0) NextWeapon();
                        else PrevWeapon();
                    }
                    else AdjustTargetAngle(rotate);
                }
            }
            else
            {
                m_angle_skip = false;
                float snap_angle = 2.0f * PI / m_weaponCount;
                float k = m_angle_target / snap_angle;
                while (k >= 1.0f) k -= 1.0f;
                if (k >= 0.5f) m_angle_target += (1.0f - k) * snap_angle;
                else m_angle_target -= k * snap_angle;
            }
            if (m_angle_current != m_angle_target)
            {
                float dist = m_angle_target - m_angle_current;
                float rotate = dist / 0.15f;
                if (fabsf(dist) < PI / 16) m_angle_skip = false;
                if (fabsf(rotate) < PI / 512.0f) m_angle_current = m_angle_target;
                else m_angle_current += rotate * time;
            }
            else m_angle_skip = false;
            RotateWeapons(m_angle_current);
            int item = AngleToWeapon(m_angle_current);
            if (item != m_selected) SelectWeapon(item);
        }

        void UpdateSliderInput()
        {
            VisualControlSlider slider = m_slider_level;
            if (slider.GetSelected())
            {
                int level = IntRemap1(slider.GetPercent(), 0, 100, 0, 2);
                if (level != m_level)
                {
                    SelectLevel(level);
                    __MGR_SOUNDS().play(SFX_MENU_SLIDER_MOVE);
                }
            }
            else
            {
                int current = slider.GetPercent();
                int target = IntRemap1(m_level, 0, 3 - 1, 0, 100);
                if (current < target) slider.SetPercent(current + MAX(1, (target - current) / 4));
                else if (current > target) slider.SetPercent(current - MAX(1, (current - target) / 4));
            }
        }

        void UpdateHighlight(float time)
        {
            const float speed = 1.0f / 0.3f;
            for (int i = 0; i < m_weaponCount; i++)
            {
                m_highlight[i] = CLAMP(m_highlight[i] + (i == m_selected ? speed : -speed) * time, 0.0f, 1.0f);
                m_weapons[i].setShopHighlight(m_highlight[i]);
                if (m_weapons[i].getType() == WT_Hydra && m_hydraPool != null)
                    setEmissiveColor(m_hydraPool, new SColor(0xAAAAAAAA).getInterpolated(new SColor(0x00000000), m_highlight[i]));
            }
        }
    }
}
