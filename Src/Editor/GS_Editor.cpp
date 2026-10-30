#include "stdafx.h"
#include "GS_Editor.h"
#include "Game/Playfield.h"
#include "Game/Config/GameConfig.h"
#include "GameStates/GSLevel.h"
#include "Game/Objects/Unit.h"
#include <map>
#include <set>
#include <cassert>
#include <cstdarg>
#include <cfloat>

bool g_isInEditor = false;
GS_Editor* g_editor = 0;

GS_Editor::GS_Editor ()
{
	m_icons = 0;
	m_firstStart = true;
	m_copiedDay = 0;
}

GS_Editor::~GS_Editor ()
{
	if (m_icons)
		m_icons->drop();
}

IGUISpriteBank* uiCreateSprites ()
{
	if (!g_unitTypes.empty())
	{
		IGUISpriteBank* sprites = __IRR_GUI()->addEmptySpriteBank("editor ui icons");
		sprites->addTexture(GS_Editor::GetInstance()->m_icons);
		for (int i = 0; i < g_unitTypes.size(); ++i)
		{
			SGUISprite sprite;
			SGUISpriteFrame frame;
			frame.textureNumber = 0;
			frame.rectNumber = i;
			sprite.Frames.push_back(frame);
			sprite.frameTime = 0;
			sprites->getSprites().push_back(sprite);
			sprites->getPositions().push_back(g_unitTypes[i]->attributes->getAttributeAsRect("IconRect"));
		}
		return sprites;
	}
	return 0;
}

bool GS_Editor::init ()
{
	if (m_icons) return true;

	const char* icons = "icons.png";
	m_icons = __IRR_DRIVER()->getTexture(icons);
	if (!m_icons)
	{
		Logs::error("Could not load editor icons: %s\n", icons);
		return false;
	}
	m_icons->grab();
	m_waveEditId = uiGenId();
	return true;
}

void GS_Editor::enter ()
{
	g_editor = this;

	__APP()->m_fpstext->setVisible(false);

	if (m_firstStart)
	{
		m_firstStart = false;
		load();

		__IRR_GUI()->getSkin()->setFont(__IRR_GUI()->getFont("font.png"));
		m_root = __IRR_GUI()->addTab(r2i(0, 0, 0, 0), __IRR_GUI()->getRootGUIElement());
	}
	else
	{
		__IRR_GUI()->getRootGUIElement()->addChild(m_root);
		m_root->drop();
	}

	if (CPlayfield::GetInstance()->isValid())
	{
		// get waves
		const array<SUnitAtTime>& waves = CPlayfield::GetInstance()->getWaves();
		m_unitsCurLev.clear();
		for (int i = 0; i < waves.size(); ++i)
		{
			TimeUnitRow tur = { waves[i].time, waves[i].type, -1 };
			m_unitsCurLev.push_back(tur);
		}
	}
}

void GS_Editor::exit ()
{
	save();
	g_editor = 0;
	__APP()->m_fpstext->setVisible(true);
	m_root->grab();
	m_root->remove();
}

void GS_Editor::uiWave (Wave* wave)
{
	uiStaticText(L"name:", AUTO_SIZE);
	uiEditString(&wave->name, &wave->name, AUTO_SIZE, 15);
	uiLayoutSpace(5);

	uiCheckbox(&wave->packed, &wave->packed, AUTO_SIZE, L"packed");

	uiLayoutNextRow();

	uiStaticText(L"time:", AUTO_SIZE);
	uiEditInt(&wave->timeStart, &wave->timeStart, AUTO_SIZE, 0, 999, 3, false);
	uiEditInt(&wave->timeInterval, &wave->timeInterval, AUTO_SIZE, 0, 999, 3, false);
	uiLayoutSpace(5);

	uiStaticText(L"interval:", AUTO_SIZE);
	uiEditInt(&wave->repeatInterval, &wave->repeatInterval, AUTO_SIZE, 0, 999, 3, false);
	uiLayoutSpace(5);

	uiStaticText(L"count:", AUTO_SIZE);
	uiEditInt(&wave->repeatCount, &wave->repeatCount, AUTO_SIZE, 1, 99, 2, false);

	uiLayoutNextRow();
	uiLayoutNextRow();

	for (int i = 0; i < wave->variants.size(); ++i)
	{
		Variant* var = wave->variants[i];

		if (uiButton(UIID(var, 1), L"-", AUTO_SIZE))
		{
			deleteVariant(wave, i--);
			uiLayoutNextRow();
			continue;
		}

		uiStaticText((stringw(L"variant ") + stringw(i + 1)).c_str(), AUTO_SIZE);

		uiLayoutSpace(10);
		uiCheckbox(&var->intro, &var->intro, AUTO_SIZE, L"intro camera");

		uiLayoutNextRow();

		for (int j = 0; j < var->enemyTypes.size(); ++j)
		{
			EnemyType* enemy = var->enemyTypes[j];

			if (uiButton(UIID(enemy, 1), L"-", AUTO_SIZE))
			{
				deleteEnemyType(var, j--);
				uiLayoutNextRow();
				continue;
			}

			uiStaticText(L"count:", AUTO_SIZE);
			uiEditInt(&enemy->minCount, &enemy->minCount, AUTO_SIZE, 1, 99, 2, false);
			if (enemy->maxCount < enemy->minCount) enemy->maxCount = enemy->minCount;
			uiEditInt(&enemy->maxCount, &enemy->maxCount, AUTO_SIZE, 1, 99, 2, false);
			if (enemy->maxCount < enemy->minCount) enemy->minCount = enemy->maxCount;
			uiLayoutSpace(5);

			uiStaticText(L"type:", AUTO_SIZE);

			static int selected = 0;
			for (int i = 0; i < g_unitTypes.size(); ++i)
			{
				if (g_unitTypes[i]->attributes->getAttributeAsBool("HideInEditor"))
					continue;
				bool state = i == enemy->unitType;
				r2i rc = g_unitTypes[i]->attributes->getAttributeAsRect("IconRect");
				uiPushButton(UIID(&enemy->unitType, -1, i), &state, L"", uiLayoutAutoWidth(20), true, 0, m_icons, rc);
				uiLayoutSpace(1);
				if (state) enemy->unitType = i;
			}
			uiLayoutSpace(4);
			uiStaticText(g_unitTypes[enemy->unitType]->name.c_str(), AUTO_SIZE);

			uiLayoutNextRow();
		}

		if (uiButton(UIID(var, 2), L"new enemy type", AUTO_SIZE))
		{
			EnemyType* enemy = new EnemyType;
			enemy->unitType = 0;
			enemy->minCount = 3;
			enemy->maxCount = 5;
			var->enemyTypes.push_back(enemy);
		}
		uiLayoutNextRow();
		uiLayoutNextRow();
	}

	if (uiButton(&wave->variants, L"new variant", AUTO_SIZE))
	{
		Variant* var = new Variant;
		var->intro = false;
		wave->variants.push_back(var);
	}
}

bool canVisualizeWaves (int level, int day)
{
	return CPlayfield::GetInstance()->isValid() &&
		CPlayfield::GetInstance()->getWaves().size() > 0 &&
		CPlayfield::GetInstance()->m_CurrentDay == day &&
		CPlayfield::GetInstance()->m_CurrentLevel == level;
}

UnitIconState getUnitIconState (int pos, int wavePos, bool alive)
{
	if (pos == wavePos) return UIS_NextToSpawn;
	if (pos > wavePos) return UIS_Future;
	if (alive) return UIS_Alive;
	return UIS_Dead;
}

void uiVisualizeWaves (float timeScale, r2i rc)
{
	std::set<int> livingUnits;
	const TUnitList& units = CPlayfield::GetInstance()->getUnits();
	for (TUnitList::ConstIterator it = units.begin(); it != units.end(); it++)
		livingUnits.insert((*it)->m_id);

	const array<SUnitAtTime>& waves = CPlayfield::GetInstance()->getWaves();
	int wavePos = CPlayfield::GetInstance()->getWavePos();

	static int unitIconTimeline;
	uiBeginUnitIconTimeline(&unitIconTimeline, timeScale, rc);
	for (int i = 0; i < waves.size(); ++i)
	{
		SUnitAtTime unit = waves[i];
		UnitIconState state = getUnitIconState(i, wavePos, livingUnits.find(i << 8) != livingUnits.end());
		uiUnitIconTimelineItem(&unitIconTimeline, unit.time, unit.type, state);
	}
	for (int i = 0; i < CPlayfield::GetInstance()->m_extraSpawns.size(); ++i)
	{
		UnitTypeAndId unit = CPlayfield::GetInstance()->m_extraSpawns[i];
		UnitIconState state = getUnitIconState(0, 1, livingUnits.find(unit.id) != livingUnits.end());
		uiUnitIconTimelineItem(&unitIconTimeline, unit.time, unit.unitType, state);
	}
	uiEndUnitIconTimeline(&unitIconTimeline, CPlayfield::GetInstance()->getTime());
}

void GS_Editor::uiWavesTab ()
{
	// --- level list ---------------------------------------------------------
	uiBeginList(&m_currentLevel, r2i(0, 0, 100, 150));
	for (int i = 0; i < m_levels.size(); ++i)
		uiListItem(&m_currentLevel, i == m_levels.size() - 1 ? L"endless" : (stringw(L"level ") + stringw(i + 1)).c_str());
	uiEndList(&m_currentLevel, &m_currentLevel);

	// --- level edit buttons -------------------------------------------------
	uiLayoutBegin(p2i(3, 150), 20);

	static int newLevelId;
	if (uiButton(&newLevelId, L"new", AUTO_SIZE))
		insertNewLevel();
	uiLayoutSpace(3);
	static int deleteLevelId;
	if (uiButton(&deleteLevelId, L"delete", AUTO_SIZE, m_currentLevel >= 0))
		deleteLevel(m_currentLevel);
	uiLayoutNextRow();
	static int upLevelId;
	if (uiButton(&upLevelId, L"up", AUTO_SIZE, m_currentLevel > 0))
	{
		std::swap(m_levels[m_currentLevel], m_levels[m_currentLevel - 1]);
		--m_currentLevel;
	}
	uiLayoutSpace(3);
	static int downLevelId;
	if (uiButton(&downLevelId, L"down", AUTO_SIZE, m_currentLevel < m_levels.size() - 1))
	{
		std::swap(m_levels[m_currentLevel], m_levels[m_currentLevel + 1]);
		++m_currentLevel;
	}

	if (m_currentLevel >= 0)
	{
		EditorLevel& level = m_levels[m_currentLevel];

		const wchar_t* difficulties[] = { L"easy", L"medium", L"hard" };
		int counts[3] = { 0 };

		// --- day list -------------------------------------------------------
		uiBeginList(&m_currentDay, r2i(0, 192, 100, uiParentHeight() - 64));
		for (int i = 0; i < level.days.size(); ++i)
		{
			stringw name = editingEndless() ? 
				stringw(difficulties[level.days[i]->difficulty]) + L" " + stringw(++counts[level.days[i]->difficulty]) :
				stringw(L"day ") + stringw(i + 1);
			uiListItem(&m_currentDay, name.c_str());
		}
		uiEndList(&m_currentDay, &m_currentDay);

		// --- day buttons ----------------------------------------------------
		uiLayoutBegin(p2i(3, uiParentHeight() - 64), 20);
		static int newDayId;
		if (uiButton(&newDayId, L"new", AUTO_SIZE))
			insertNewDay();
		uiLayoutSpace(3);
		static int deleteDayId;
		if (uiButton(&deleteDayId, L"cut", AUTO_SIZE, m_currentDay >= 0))
		{
			if (m_copiedDay) delete m_copiedDay;
			m_copiedDay = cloneDay(level.days[m_currentDay]);
			deleteDay(m_currentLevel, m_currentDay);
		}
		uiLayoutNextRow();
		static int upDayId;
		if (uiButton(&upDayId, L"up", AUTO_SIZE, m_currentDay > 0))
		{
			std::swap(level.days[m_currentDay], level.days[m_currentDay - 1]);
			--m_currentDay;
		}
		uiLayoutSpace(3);
		static int downDayId;
		if (uiButton(&downDayId, L"down", AUTO_SIZE, m_currentDay < level.days.size() - 1))
		{
			std::swap(level.days[m_currentDay], level.days[m_currentDay + 1]);
			++m_currentDay;
		}
		uiLayoutNextRow();
		static int copyDayId;
		if (uiButton(&copyDayId, L"copy", AUTO_SIZE, m_currentDay >= 0))
		{
			if (m_copiedDay) delete m_copiedDay;
			m_copiedDay = cloneDay(level.days[m_currentDay]);
		}
		uiLayoutSpace(3);
		static int pasteDayId;
		if (uiButton(&pasteDayId, L"paste", AUTO_SIZE, m_copiedDay != 0))
		{
			insertNewDay(cloneDay(m_copiedDay));
		}

		if (m_currentDay >= 0)
		{
			Day* day = level.days[m_currentDay];

			// --- level end time ---------------------------------------------
			uiLayoutBegin(p2i(102, 0), 20);
			uiStaticText(L"level end time:", AUTO_SIZE);
			uiEditInt(&day->dayEndTime, &day->dayEndTime, AUTO_SIZE, 1, 999, 3, false);

			
			if (editingEndless())
			{
				// --- endless segment difficulty -----------------------------
				uiStaticText(L"difficulty:", AUTO_SIZE);
				for (int i = 0; i < 3; ++i)
				{
					bool state = i == day->difficulty;
					uiPushButton(UIID(&day->difficulty, i), &state, difficulties[i], AUTO_SIZE);
					if (state) day->difficulty = i;
				}
			}
			else
			{
				// --- challenge ----------------------------------------------
				uiStaticText(L"challenge:", AUTO_SIZE);
				uiBeginComboBox(&day->challenge, uiLayoutAutoWidth(250));
				for (int i = 0; i < CT_Count; ++i)
					uiComboBoxItem(&day->challenge, ChallengeTypeToString(ChallengeType(i)));
				uiEndComboBox(&day->challenge, (int*)&day->challenge);
			}

			bool inLevel = !editingEndless() && canVisualizeWaves(m_currentLevel, m_currentDay);
			static bool currentDay = false;

			// --- wave list and timeline -------------------------------------
			int h = (inLevel && currentDay) ? uiParentHeight() / 4 : uiParentHeight() / 2;
			uiBeginList(&day->waves, r2i(100, 21, uiParentWidth(), h));
			for (int i = 0; i < day->waves.size(); ++i)
			{
				int icon = day->waves[i]->variants.size() > 0 &&
					day->waves[i]->variants[0]->enemyTypes.size() > 0 ?
					day->waves[i]->variants[0]->enemyTypes[0]->unitType : -1;
				uiListItem(&day->waves, day->waves[i]->name.c_str(), icon);
			}
			uiBeginTimeline(UIID(&day->waves, 1), day->dayEndTime, r2i(100, 0, uiParentWidth() - 15, 4000));
			for (int i = 0; i < day->waves.size(); ++i)
				uiTimelineItem(UIID(&day->waves, 1), day->waves[i]);
			uiEndTimeline(UIID(&day->waves, 1), &m_currentWave);
			uiEndList(&day->waves, &m_currentWave);

			// --- wave edit area ---------------------------------------------
			uiLayoutBegin(p2i(102, uiParentHeight() / 2), 20);

			if (uiButton(m_waveEditId.setItem(0), L"new", AUTO_SIZE))
				insertNewWave();
			uiLayoutSpace(2);

			if (uiButton(m_waveEditId.setItem(1), L"up", AUTO_SIZE, m_currentWave > 0))
			{
				std::swap(day->waves[m_currentWave - 1], day->waves[m_currentWave]);
				--m_currentWave;
			}
			uiLayoutSpace(2);

			if (uiButton(m_waveEditId.setItem(2), L"down", AUTO_SIZE, m_currentWave >= 0 && m_currentWave < day->waves.size() - 1))
			{
				std::swap(day->waves[m_currentWave + 1], day->waves[m_currentWave]);
				++m_currentWave;
			}
			uiLayoutSpace(2);

			if (uiButton(m_waveEditId.setItem(3), L"delete", AUTO_SIZE, m_currentWave >= 0))
				deleteWave(m_currentLevel, m_currentDay, m_currentWave);
			uiLayoutSpace(2);

			if (uiButton(m_waveEditId.setItem(4), L"clone", AUTO_SIZE, m_currentWave >= 0))
				cloneWave(day->waves[m_currentWave]);
			uiLayoutSpace(2);

			if (inLevel)
				uiCheckbox(&currentDay, &currentDay, AUTO_SIZE, L"current day");

			if (inLevel && currentDay)
			{
				uiVisualizeWaves(day->dayEndTime, r2i(200, h, uiParentWidth() - 15, 2*h));
			}

			uiLayoutNextRow();

			if (m_currentWave >= 0)
			{
				uiWave(day->waves[m_currentWave]);
			}
		}
	}
}

void GS_Editor::uiWeaponsTab ()
{
	// --- level list ---------------------------------------------------------
	static int listId;
	uiBeginList(&listId, r2i(0, 0, 100, uiParentHeight()));
	for (int i = 0; i < m_levels.size(); ++i)
		uiListItem(&listId, i == m_levels.size() - 1 ? L"endless" : (stringw(L"level ") + stringw(i + 1)).c_str());
	uiEndList(&listId, &m_currentLevel);

	if (m_currentLevel >= 0)
	{
		EditorLevel& level = m_levels[m_currentLevel];
		// --- weapons --------------------------------------------------------
		uiLayoutBegin(p2i(102, 2), 20);
		for (int i = 0; i < WT_Count - 1; ++i)
		{
			
			const wchar_t* weapons[] = { L"gate", L"hydra", L"fireball", L"iceball", L"griffin" };
			uiStaticText(weapons[i], uiLayoutAutoWidth(100));
			uiStaticText(L"level: ", AUTO_SIZE);
			const wchar_t* levels[] = { L"0", L"1", L"2", L"3" };
			for (int j = -1; j < 3; ++j)
			{
				bool state = j == level.levelInfo->weaponStartingLevel[i];
				uiPushButton(UIID(&level.levelInfo->weaponStartingLevel[i], j), &state, levels[1 + j], AUTO_SIZE);
				uiLayoutSpace(10);
				if (state) level.levelInfo->weaponStartingLevel[i] = (EWeaponLevel)j;
			}
			uiLayoutSpace(30);
			uiCheckbox(&level.levelInfo->weaponAvailableInShop[i], &level.levelInfo->weaponAvailableInShop[i], AUTO_SIZE, L"shop");
			uiLayoutNextRow();
		}

		// --- spells ---------------------------------------------------------
		uiStaticText(L"spell: ", AUTO_SIZE);
		uiCheckbox(&level.levelInfo->spellBought, &level.levelInfo->spellBought, AUTO_SIZE, L"bought");
		uiLayoutNextRow();
		const wchar_t* spells[] = { L"none", L"boulder", L"lightning", L"ghost army", L"tornado" };
		for (int j = -1; j < ST_Count; ++j)
		{
			bool state = j == level.levelInfo->spellAvailableInShop;
			uiLayoutSpace(10);
			uiPushButton(UIID(&level.levelInfo->spellAvailableInShop, j), &state, spells[1 + j], AUTO_SIZE);
			uiLayoutNextRow();
			if (state) level.levelInfo->spellAvailableInShop = (ESpellType)j;
		}
	}
}

void GS_Editor::uiModelsTab ()
{
	TAttributesMap& modelsMap = CGameConfig::GetInstance()->getAttributesMap(L"model");
	uiBeginList(&modelsMap, r2i(0, 0, 200, uiParentHeight() / 2));
	for (TAttributesMap::Iterator it = modelsMap.getIterator(); !it.atEnd(); it++)
		uiListItem(&modelsMap, it->getKey().c_str());
	uiEndList(&modelsMap, &m_selectedModel);

	if (m_selectedModel >= 0)
	{
		int index = 0;
		TAttributesMap::Iterator model = modelsMap.getIterator();
		for (; !model.atEnd(); model++, index++)
			if (index == m_selectedModel)
				break;

		while (m_modelNodes.size() < modelsMap.size())
			m_modelNodes.push_back(0);

		IAttributes* attr = model->getValue();
		if (m_modelNodes[m_selectedModel] == 0)
		{
			// load node
			m_modelNodes[m_selectedModel] = uiGetSceneManager()->loadColladaScene(attr->getAttributeAsString("File").c_str());
			float scale = attr->getAttributeAsFloat("Scale");
			m_modelNodes[m_selectedModel]->setScale(v3f(scale, scale, scale));
			uiGetSceneManager()->getRootSceneNode()->addChild(m_modelNodes[m_selectedModel]);
			m_modelNodes[m_selectedModel]->setVisible(false);
			if (!m_modelNodes[m_selectedModel]->getAnimators().empty())
			{
				m_modelNodes[m_selectedModel]->getAnimators().getFirstElem()->getTimelineCtrl()->setLoop(true);
				((collada::ISceneNodeAnimator*)m_modelNodes[m_selectedModel]->getAnimators().getFirstElem())->setInterpolationMode(collada::ISceneNodeAnimator::EIT_STEP);
			}
			cleanNode(m_modelNodes[m_selectedModel], true);
		}

		// set current node as visible, all other as not visible
		for (int i = 0; i < m_modelNodes.size(); ++i)
			if (m_modelNodes[i])
				m_modelNodes[i]->setVisible(i == m_selectedModel);

		// animation list
		if (!m_modelNodes[m_selectedModel]->getAnimators().empty())
		{
			ITimelineController* ctrl = m_modelNodes[m_selectedModel]->getAnimators().getFirstElem()->getTimelineCtrl();
			int selectedAnim = ctrl->getCurrentClipIndex();
			uiBeginList(ctrl, r2i(0, uiParentHeight() / 2, 200, uiParentHeight()));
			for (int i = 0; i < ctrl->getClipCount(); ++i)
				uiListItem(ctrl, stringw(ctrl->getClipName(i)).c_str());
			uiEndList(ctrl, &selectedAnim);
			if (selectedAnim != ctrl->getCurrentClipIndex())
				ctrl->setClip(selectedAnim);
		}

		// set aspect ratio and viewport
		ISceneManager* mgr = uiGetSceneManager();
		p2i pos = uiParentAbsolutePos() + p2i(202, 20);
		m_viewport = r2i(pos, d2i(uiParentWidth() - 202, uiParentHeight() - 20));
		mgr->getActiveCamera()->setAspectRatio(m_viewport.getWidth() / (float)m_viewport.getHeight());
	}
}

void GS_Editor::update ()
{
	for (int i = 0; i < 2; ++i)
	{
		uiBegin(m_root);

		d2i screen = __IRR_DRIVER()->getScreenSize();
		m_activeTab = uiBeginTabs(&m_activeTab, r2i(5, 5, screen.Width - 5, screen.Height - 5),
			L"waves", L"weapons", L"models", 0);
		if (m_activeTab == 0) uiWavesTab();
		if (m_activeTab == 1) uiWeaponsTab();
		if (m_activeTab == 2) uiModelsTab();
		uiEndTabs(&m_activeTab);

		static int playLevel;
		if ((m_activeTab == 0 || m_activeTab == 1) && m_currentLevel >= 0 && (m_currentDay >= 0 || m_activeTab == 1) &&
			uiButton(&playLevel, L"play", r2i(p2i(uiParentWidth() - 180, uiParentHeight() - 25), d2i(40, 20))))
		{
			CPlayfield* playfield = CPlayfield::GetInstance();

			if (m_activeTab == 1 || m_currentDay == 0)
			{
				playfield->StartNewLevel( m_currentLevel );
			}
			else
			{
				playfield->m_CurrentLevel = m_currentLevel;
				playfield->m_CurrentDay = m_currentDay;
			
				playfield->WriteProfileStats();

				__MGR_STATES()->switchState<GSLevel>();
			}
			exit();
		}

		static int killCastle;
		if (CPlayfield::GetInstance()->isValid() &&
			uiButton(&killCastle, L"DIE!", r2i(p2i(uiParentWidth() - 135, uiParentHeight() - 25), d2i(40, 20))))
		{
			bool invulnerable = CGameConfig::Invulnerable;
			CGameConfig::Invulnerable = false;
			CPlayfield::GetInstance()->getGate()->damageGate(99999);
			CGameConfig::Invulnerable = invulnerable;
		}

		static int saveId;
		if (uiButton(&saveId, L"save", r2i(p2i(uiParentWidth() - 90, uiParentHeight() - 25), d2i(40, 20))))
			save();

		static int exitId;
		if (uiButton(&exitId, L"exit", r2i(p2i(uiParentWidth() - 45, uiParentHeight() - 25), d2i(40, 20))))
			exit();

		uiEnd();
	}

	if (uiGetSceneManager())
	{
		uiGetSceneManager()->update();
	}
}

void GS_Editor::draw ()
{
	__IRR_DRIVER()->beginScene();
	__IRR_DRIVER()->setClearColor(SColor(255,0,128,192));
	__IRR_DRIVER()->clearBuffers(EFB_COLOR | EFB_DEPTH);

	__IRR_DRIVER()->beginScene2D();
	__IRR_GUI()->drawAll();
	__IRR_DRIVER()->endScene2D();

	// draw scene
	if (uiGetSceneManager() && m_activeTab == 2)
	{
		r2i viewport = __IRR_DRIVER()->getViewPort();
		__IRR_DRIVER()->setViewPort(m_viewport);
		uiGetSceneManager()->drawAll();
		
		uiDrawGridIfGroundVisible();

		__IRR_DRIVER()->setViewPort(viewport);
	}

	__IRR_DRIVER()->endScene();
}

bool GS_Editor::OnEvent (const SEvent& ev)
{
	uiNotifyEvent(ev, m_activeTab == 2 ? m_viewport : r2i(0, 0, 0, 0));
	return false;
}

void GS_Editor::insertNewWave ()
{
	Day* day = m_levels[m_currentLevel].days[m_currentDay];

	Wave* wave = new Wave;
	wave->name = stringw(L"wave") + stringw(day->waves.size() + 1);
	wave->timeStart = 0;
	wave->timeInterval = 5;
	wave->repeatInterval = 20;
	wave->repeatCount = 1;
	wave->packed = true;

	day->waves.push_back(wave);
	m_currentWave = day->waves.size() - 1;
}

void GS_Editor::insertNewDay (Day* level)
{
	if (level == 0)
	{
		level = new Day;
		level->dayEndTime = 100;
		level->challenge = CT_None;
		level->difficulty = 0;
	}

	m_levels[m_currentLevel].days.push_back(level);
	m_currentDay = m_levels[m_currentLevel].days.size() - 1;
}

void GS_Editor::cloneWave (Wave* wave)
{
	m_levels[m_currentLevel].days[m_currentDay]->waves.push_back(::cloneWave(wave));
	m_currentWave = m_levels[m_currentLevel].days[m_currentDay]->waves.size() - 1;
}

void GS_Editor::save ()
{
	array<int> daysPerLevel;
	for (int i = 0; i < m_levels.size(); ++i)
		daysPerLevel.push_back(m_levels[i].days.size());
	saveNumberOfDaysPerLevel(daysPerLevel);

	for (int i = 0; i < m_levels.size(); ++i)
	{
		saveLevelInfo(getLevelPath(i).c_str(), m_levels[i].levelInfo);
		for (int j = 0; j < m_levels[i].days.size(); ++j)
		{
			saveDayInfo(getDayPath(i, j).c_str(), m_levels[i].days[j]);
		}
	}
}

void GS_Editor::load ()
{
	assert(m_levels.size() == 0);
	array<int> daysPerLevel = getNumberOfDaysPerLevel();

	for (int i = 0; i < daysPerLevel.size(); ++i)
	{
		EditorLevel lev;
		lev.levelInfo = loadLevelInfo(getLevelPath(i).c_str());
		m_levels.push_back(lev);
		for (int j = 0; j < daysPerLevel[i]; ++j)
		{
			m_levels[i].days.push_back(loadDayInfo(getDayPath(i, j).c_str()));
			if (m_levels[i].days[j] == 0)
			{
				m_levels[i].days[j] = new Day;
				m_levels[i].days[j]->dayEndTime = 100;
			}
		}
	}
}

void GS_Editor::deleteEnemyType (Variant* variant, int type)
{
	delete variant->enemyTypes[type];
	variant->enemyTypes.erase(type);
}

void GS_Editor::deleteVariant (Wave* wave, int variant)
{
	delete wave->variants[variant];
	wave->variants.erase(variant);
}

void GS_Editor::deleteDay (int lvl, int day)
{
	delete m_levels[lvl].days[day];
	m_levels[lvl].days.erase(day);

	if (lvl == m_currentLevel && day == m_currentDay)
	{
		if (m_levels[lvl].days.empty())
			m_currentDay = -1;
		else if (m_currentDay >= m_levels[lvl].days.size())
			m_currentDay = m_levels[lvl].days.size() - 1;
	}
}

void GS_Editor::deleteWave (int lvl, int day, int wave)
{
	Day* level = m_levels[lvl].days[day];
	delete level->waves[wave];
	level->waves.erase(wave);

	if (lvl == m_currentLevel && day == m_currentDay && wave == m_currentWave)
	{
		if (level->waves.empty())
		{
			m_currentWave = -1;
		}
		else if (m_currentWave >= level->waves.size())
		{
			m_currentWave = level->waves.size() - 1;
		}
	}
}

void GS_Editor::insertNewLevel ()
{
	EditorLevel lvl;
	lvl.levelInfo = new Level;
	lvl.levelInfo->spellAvailableInShop = ST_None;
	for (int i = 0; i < WT_Count - 1; ++i)
	{
		lvl.levelInfo->weaponAvailableInShop[i] = true;
		lvl.levelInfo->weaponStartingLevel[i] = WL_None;
	}
	m_levels.push_back(lvl);
	m_currentLevel = m_levels.size() - 1;
}

void GS_Editor::deleteLevel (int lvl)
{
	while (!m_levels[lvl].days.empty())
		deleteDay(lvl, 0);
	delete m_levels[lvl].levelInfo;
	m_levels.erase(lvl);

	if (lvl == m_currentLevel)
	{
		if (m_levels.empty())
			m_currentLevel = -1;
		else if (m_currentLevel >= m_levels.size())
			m_currentLevel = m_levels.size() - 1;
	}
}

void GS_Editor::allocateRows ()
{
	std::map<int, float> sweep;
	for (int i = 0; i < m_unitsCurLev.size(); ++i)
	{
		// find an empty row
		int row = 0;
		for (; sweep.find(row) != sweep.end() && sweep[row] > m_unitsCurLev[i].time; ++row)
		{
		}
		sweep[row] = m_unitsCurLev[i].time + 2.0f;
		m_unitsCurLev[i].row = row;
	}
}

bool GS_Editor::editingEndless ()
{
	return m_currentLevel == m_levels.size() - 1;
}
