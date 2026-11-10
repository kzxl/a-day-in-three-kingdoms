# Deep Officialdom & Autonomous Fief Governance Specification

## 1. Overview
The Deep Officialdom system expands political progression beyond static rank badges into an interactive governance loop. When holding rank 3 (Phẩm 3) or higher under a lord, the player can be appointed governor (*Thái Thú*) of an allied citadel. In this role, the player manages demographics, local garrison, military armories, appoints an autonomous administrative cabinet, invests in late-game megaprojects, deploys garrison troops on strategic military expeditions, handles emergent regional crises, and commands citadel siege defenses.

---

## 2. Architecture & Data Contracts

### 2.1 Database Schema
Located in `0708-governor-aides-and-monthly-digest.sql`, `0709-fief-megaprojects-and-expeditions.sql`, and `0710-fief-crises-and-siege-defense.sql`:

1. **`po_governor_fief` extensions:**
   - `civil_aide_npc_id` (`INTEGER NOT NULL DEFAULT 0`): NPC ID appointed as Civil Aide (*Trưởng Thừa*).
   - `military_aide_npc_id` (`INTEGER NOT NULL DEFAULT 0`): NPC ID appointed as Military Aide (*Đô Úy*).
   - `policy_directive` (`TEXT NOT NULL DEFAULT 'BALANCE'`): Current autonomous governance directive (`AGRI`, `COMM`, `MILITARY`, `BALANCE`).
   - `last_monthly_digest_text` (`TEXT NOT NULL DEFAULT ''`): Cache of the most recent month's summary.

2. **`po_governance_digest` table:**
   - Stores chronological monthly records:
     - `citadel_id`, `year`, `month`, `directive`
     - `population_delta`, `treasury_delta`, `food_consumed`, `garrison_delta`, `merits_earned`
     - `civil_action_vi`, `military_action_vi`, `summary_vi`

3. **`po_fief_megaproject` table:**
   - `citadel_id`, `project_code`, `status`, `invested_copper`, `required_copper`, `completed_day`

4. **`po_fief_expedition_log` table:**
   - `id`, `citadel_id`, `day_index`, `expedition_type`, `troops_dispatched`, `casualties`, `spoils_copper`, `merits_earned`, `narrative_vi`

5. **`po_fief_crisis` table:**
   - `id`, `citadel_id`, `crisis_code`, `title_vi`, `description_vi`, `severity`, `status`, `created_day`, `resolved_day`, `resolution_choice`, `outcome_vi`

6. **`po_fief_siege_log` table:**
   - `id`, `citadel_id`, `day_index`, `attacker_faction_name`, `attacker_troops`, `defense_troops`, `tactical_stance`, `outcome`, `defense_casualties`, `attacker_casualties`, `spoils_copper`, `merits_earned`, `battle_phases_vi`, `narrative_vi`

### 2.2 Core Service & Autonomy Loop
- **Event Wire-up**: `PowerModule.cs` subscribes `courtService.SimulateMonthlyFiefAutonomy(closed.Date)` to `MonthClosed`.
- **Strategic Directives**:
  - `AGRI` (*Trọng Nông Tích Cốc*): Boosts agriculture score and population growth (+2%), increases food reserves, small local treasury expenditure (-40 Quan).
  - `COMM` (*Trọng Thương Thu Thuế*): Boosts commerce score, generates high monthly treasury taxes (+120 Quan), modest population influx (+1%).
  - `MILITARY` (*Quân Bị Tiễu Phỉ*): Trains and expands citadel garrison (+250 troops), reinforces fortifications (+6) and public order (+5), reduces unrest.
  - `BALANCE` (*Cân Bằng Toàn Diện*): Moderate all-around gains (+3 to all attributes, +35 Quan, +1% population).
- **Aide Personality & Trait Dynamics**:
  - `ThamTai` / `XaoQuyet`: Trưởng Thừa embezzles treasury copper and raises civil unrest (+2 unrest).
  - `ThanhLiem`: Clean audit boosts treasury savings (+30 Quan) and decreases unrest (-3 unrest).
  - `KeoKiet`: Strict rations reduce monthly grain consumption by 25%.
  - `HieuHoc`: Civil scholar earns extra court merits (+3 merits).
  - `TrungNghia` / `CanDam`: Đô Úy trains higher quality recruits (+40 garrison, +3 merits).
  - `NongNay`: Strict corporal discipline raises garrison drill but slight unrest (+1 unrest).
  - `DiemTinh`: Methodical defensive formations permanently strengthen wall defense (+3).

---

## 3. Citadel Expeditions & Fief Megaprojects

### 3.1 Citadel Military Expeditions (`po_fief_expedition_log`)
Governors can deploy accumulated garrison troops and armory weapons into tactical military operations:
1. **`PACIFY_BANDITS` (*Tiễu Phạt Sơn Tặc*)**:
   - Requirements: 300+ garrison troops, 20+ armory weapons.
   - Effects: Clears local mountain bandits, reduces unrest by -12%, captures 200–350 Quan in spoils for local treasury, awards +25 court merits.
2. **`BORDER_PATROL` (*Thao Diễn Biên Ải*)**:
   - Requirements: 400+ garrison troops, 20+ armory weapons.
   - Effects: Fortifies mountain passes, boosts citadel defense by +8, increases reputation by +3 and merits by +15.
3. **`SUPPORT_LORD` (*Chi Viện Tiền Tuyến Minh Chủ*)**:
   - Requirements: 800+ garrison troops, 40+ weapons, allegiance to a lord.
   - Effects: Rushes reinforcements to the lord's frontline (+715 troops to warlord faction army), increases lord trust (+15) and awards +60 court merits.

### 3.2 Fief Megaprojects (`po_fief_megaproject`)
Endgame capital sinks allowing governors to permanently transform their regional seat:
1. **`CANAL` (*Đại Kênh Thủy Lợi*)** - Cost: 1,500 Quan:
   - Permanently grants +15 Agriculture and +1% monthly demographic growth.
2. **`ACADEMY` (*Thái Học Viện*)** - Cost: 2,000 Quan:
   - Grants +5 Merits every month and +2 Reputation quarterly.
3. **`ARMORY` (*Đại Dã Thần Binh Xưởng*)** - Cost: 2,500 Quan:
   - Expands weapon armory capacity by +100 units and grants +50 instant weapons.
4. **`BASTION` (*Kim Thành Thang Trì*)** - Cost: 3,000 Quan:
   - Permanently grants +25 Fortification score and +20 Citadel Defense strength.

---

## 4. Emergent Fief Crises & Citadel Siege Defense

### 4.1 Emergent Regional Crises (`po_fief_crisis`)
Simulates unexpected events requiring urgent executive decisions from the governor:
- `CRISIS_FLOOD` (*Thủy Nạn Vỡ Đê*):
  - `DISPATCH_CIVIL`: Trưởng Thừa mobilizes labor to reinforce dikes (-80 Quan local treasury, +4 Agriculture).
  - `EMERGENCY_FUNDS`: Governor donates personal funds (150 Quan) to distribute relief food (+15 merits, +2 reputation, -10% unrest).
- `CRISIS_PLAGUE` (*Dịch Khí Hoành Hành*):
  - `ESTABLISH_CLINIC`: Sets up medical dispensaries (-60 Quan treasury, -8% unrest, +12 merits).
  - `QUARANTINE`: Imposes city gate lockdown (+5% unrest, saves money).
- `CRISIS_BANDIT_RAID` (*Sơn Tặc Đột Kích*):
  - `DISPATCH_MILITARY`: Đô Úy leads garrison sorties (-15 weapons, +120 Quan recovered spoils, +6 public order, +20 merits).
  - `DEFEND_WALLS`: Closes gates to protect citizens (-5 Agriculture from peripheral pillaging).
- `CRISIS_SECRET_ENVOY` (*Mật Sứ Chiêu Dụ*):
  - `BEHEAD_ENVOY`: Executes envoy and sends head to lord (+20 lord trust, +40 court merits).
  - `CONFISCATE`: Confiscates 300 Quan bribe into local treasury (+300 Quan).

### 4.2 Citadel Siege Defense Simulation (`po_fief_siege_log`)
Tactical 3-phase defense against hostile siege assaults:
- **Phases**:
  1. *Archery Volleys from Walls*
  2. *Gate Breach Defense / Boiling Oil & Fire Ambush*
  3. *Melee Defense Clashes & Officer Duels*
- **Tactical Stances**:
  - `FORTIFY_WALLS` (*Kiên Thủ Thành Quách*): Low defense casualties, consumes weapons, repels ladders and siege rams.
  - `SALLY_OUT` (*Xuất Thành Nghênh Chiến*): Cavalry counter-offensive, high enemy casualties, large spoils.
  - `AMBUSH_FIRE` (*Hỏa Công Phục Kích*): Draws enemy into burning moat traps, devastating defeat for enemy siege engines (+50 merits).

---

## 5. UI Presentation & Screens
- **Imperial Court Chamber (`GameCourtChamberControl.cs`)**:
  - Demographics & Garrison Bar: displays total population, stationed garrison troops, and weapons inventory with an active directive badge.
  - Aide Triptych: displays appointed Civil Aide (*Trưởng Thừa*) and Military Aide (*Đô Úy*).
  - Stat Matrix: 4 uniform pills for Agriculture (*Nông Khẩn*), Commerce (*Thương Nghiệp*), Fortification (*Thành Phòng*), and Public Order (*Trị An Dân Tâm*).
  - Quick action buttons for directives, developments, military expeditions, and siege simulations.
- **Dedicated Sections (`CourtScreens.cs`)**:
  - `Biến Cố Khẩn Cấp & Quyết Sách Châu Quận`: Displays pending crises and interactive resolution choices.
  - `Chiến Báo Công Thủ Thành Trì & Phản Kích`: Detailed siege battle logs with tactical phases and spoils.
  - `Đại Công Trình Phong Ấp Châu Quận`: Shows investment progress bars and funding triggers.
  - `Nhật Ký Chiến Dịch Xuất Chinh Phong Ấp`: Complete history of tactical campaigns, casualties, and spoils.
  - `Tổng Kết Chánh Vụ Phong Ấp Hàng Tháng`: Transparent monthly digest of autonomous cabinet actions.

---

## 6. Verification & Invariants
- **Deterministic Simulation**: All random outcomes use kernel `IRng` or deterministic formulas.
- **Ledger Invariance**: Local fief treasury is isolated from personal wallet, maintaining zero drift on Golden Replay hashes (`merchant-30d.json` balance 1514, `merchant-full.json` balance 778).
- **Test Coverage**: 720 passing tests across the solution.
