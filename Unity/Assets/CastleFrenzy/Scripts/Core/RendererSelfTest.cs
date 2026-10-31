// "-cf-test renderer": loads the castle, camera and a few animated units and draws sprites/fonts,
// so the BDAE/scene/2D layers can be checked in isolation (used with -cf-screenshot).
using UnityEngine;

namespace CastleFrenzy
{
    public class RendererSelfTest
    {
        static void DumpTree(ISceneNode n, int depth)
        {
            if (depth > 4) return;
            string mats = "";
            for (int i = 0; i < n.getMaterialCount(); i++)
            {
                SMaterial m = n.getMaterial(i);
                mats += " [" + m.MaterialType + " tex=" + (m.Textures[0] != null ? m.Textures[0].Name : "-") + " amb=" + m.AmbientColor + " dif=" + m.DiffuseColor + " vis=" + n.IsVisible + "]";
            }
            Debug.Log("[SelfTest] " + new string(' ', depth * 2) + n.getType() + " '" + n.Name + "' pos " + n.getAbsolutePosition() + mats);
            foreach (ISceneNode c in n.Children) DumpTree(c, depth + 1);
        }

        readonly ISceneNode m_castle, m_cameras;
        readonly ISceneNode[] m_units = new ISceneNode[4];
        readonly ASprite m_logo;
        readonly CLightSceneNode m_light;

        public RendererSelfTest()
        {
            CSceneManager smgr = CSceneManager.Instance;
            m_castle = smgr.loadColladaScene("Scenes/Castle/castle.bdae");
            smgr.getRootSceneNode().addChild(m_castle);
            CTimelineController c = m_castle.getFirstAnimator().getTimelineCtrl();
            c.setClip("sceneloop");
            c.setLoop(true);
            m_castle.getFirstAnimator().setInterpolationMode(ISceneNodeAnimator.E_INTERPOLATION_MODE.EIT_STEP);

            m_cameras = smgr.loadColladaScene("Scenes/Castle/cameras.bdae");
            smgr.getRootSceneNode().addChild(m_cameras);
            CTimelineController cc = m_cameras.getFirstAnimator().getTimelineCtrl();
            cc.setClip("sceneloop");
            cc.setLoop(false);
            CCameraSceneNode cam = (CCameraSceneNode)m_cameras.getSceneNodeFromType(ESCENE_NODE_TYPE.ESNT_COLLADA_CAMERA);
            cam.setAspectRatio(480.0f / 320.0f);
            smgr.setActiveCamera(cam);

            string[] models = { "Scenes/Goblin/goblin.bdae", "Scenes/Troll/troll.bdae", "Scenes/Skeleton/skeleton.bdae", "Scenes/Wild_boar/boar.bdae" };
            string[] clips = { "gob_vihren_walk_03", "troll_walk_01", null, null };
            ISceneNode gate = m_castle.getSceneNodeFromName("d_gate");
            v3f basePos = gate != null ? gate.getAbsolutePosition() : v3f.Zero;
            for (int i = 0; i < models.Length; i++)
            {
                m_units[i] = smgr.loadColladaScene(models[i]);
                if (m_units[i] == null) continue;
                smgr.getRootSceneNode().addChild(m_units[i]);
                m_units[i].setPosition(new v3f(-2.0f + i * 2.5f, 0.84f, 5.98f));
                m_units[i].setScale(new v3f(0.7f, 0.7f, 0.7f));
                CTimelineController uc = m_units[i].getFirstAnimator().getTimelineCtrl();
                if (clips[i] != null) uc.setClip(clips[i]);
                uc.setLoop(true);
            }

            m_light = smgr.addLightSceneNode();
            m_light.LightData.AmbientColor = new SColorf(1, 1, 1, 1);
            m_light.LightData.DiffuseColor = new SColorf(0, 0, 0, 1);

            m_logo = new ASprite();
            m_logo.Load("Menu/menu_bg");
        }

        public void Update(int ms)
        {
            CTouchPad.UpdateCounters(ms);
            CTouchElement.UpdateElements();
            CSceneManager.Instance.update(ms);
        }

        int m_frame;

        public void Draw()
        {
            CSceneManager.Instance.drawAll();
            if (++m_frame == 60)
            {
                CCameraSceneNode cam = CSceneManager.Instance.getActiveCamera();
                Debug.Log("[SelfTest] cam pos " + cam.getAbsolutePosition() + " target " + cam.getTarget() + " fov " + cam.getFOV() + " aspect " + cam.getAspectRatio() + " near " + cam.getNearValue() + " far " + cam.getFarValue());
                Debug.Log("[SelfTest] view " + cam.getViewMatrix().m + "\nproj " + cam.getProjectionMatrix().m);
                foreach (string n in new[] { "d_gate", "d_hydra", "d_griffin", "back", "front" })
                {
                    ISceneNode node = m_castle.getSceneNodeFromName(n);
                    if (node == null) { Debug.Log("[SelfTest] missing " + n); continue; }
                    Debug.Log("[SelfTest] " + n + " abs " + node.getAbsolutePosition() + " screen " + CSceneManager.Instance.getScreenCoordinatesFrom3DPosition(node.getAbsolutePosition()));
                }
                Debug.Log("[SelfTest] drawcalls " + Gfx.DrawCalls);
                DumpTree(m_castle, 0);
            }
            Gfx.beginScene2D();
            
            CFontManager.GetInstance().GetFont("LoadingTitle").DrawString("Castle Frenzy - Unity renderer test", 240, 290, CFont.HVCENTER);
            CFontManager.GetInstance().GetFont("MM_Normal").DrawString("Sprites, fonts and BDAE models", 240, 310, CFont.HVCENTER);
            Gfx.endScene2D();
        }
    }
}
