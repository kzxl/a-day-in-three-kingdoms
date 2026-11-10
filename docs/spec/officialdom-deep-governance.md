# Deep Officialdom & Autonomous Fief Governance Specification

## 1. Overview
The Deep Officialdom system expands political progression beyond static rank badges into an interactive governance loop. When holding rank 3 (Phẩm 3) or higher under a lord, the player can be appointed governor (*Thái Thú*) of an allied citadel. In this role, the player manages demographics, local garrison, military armories, and appoints an autonomous administrative cabinet.

---

## 2. Architecture & Data Contracts

### 2.1 Database Schema
Located in `0708-governor-aides-and-monthly-digest.sql`:

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

### 2.2 Core Service & Autonomy Loop
- **Event Wire-up**: `PowerModule.cs` subscribes `courtService.SimulateMonthlyFiefAutonomy(closed.Date)` to `MonthClosed`.
- **Strategic Directives**:
  - `AGRI` (*Trọng Nông Tích Cốc*): Boosts agriculture score and population growth (+2%), increases food reserves, small local treasury expenditure (-40 Quan).
  - `COMM` (*Trọng Thương Thu Thuế*): Boosts commerce score, generates high monthly treasury taxes (+120 Quan), modest population influx (+1%).
  - `MILITARY` (*Quân Bị Tiễu Phỉ*): Trains and expands citadel garrison (+250 troops), reinforces fortifications (+6) and public order (+5), reduces unrest.
  - `BALANCE` (*Cân Bằng Toàn Diện*): Moderate all-around gains (+3 to all attributes, +35 Quan, +1% population).
- **Aide Delegation**:
  - `AppointFiefAide(npcId, role)`: Appoints any living NPC as Civil Aide or Military Aide; passing `0` dismisses the aide back to local bureaucratic staff.
  - Actions and narratives dynamically highlight the appointed aide names in monthly digests.

---

## 3. UI Presentation

### 3.1 Imperial Court Chamber (`GameCourtChamberControl.cs`)
- **Fiefdom Card**:
  - Demographics & Garrison Bar: displays total population, stationed garrison troops, and weapons inventory with an active directive badge.
  - Aide Triptych: displays appointed Civil Aide (*Trưởng Thừa*) and Military Aide (*Đô Úy*).
  - Stat Matrix: 4 uniform pills for Agriculture (*Nông Khẩn*), Commerce (*Thương Nghiệp*), Fortification (*Thành Phòng*), and Public Order (*Trị An Dân Tâm*).
  - Interactive Actions: instant directive toggles, focused development projects, and quarterly evaluations.

### 3.2 Monthly Governance Digest (`CourtScreens.cs`)
- Dedicated chronology section `Tổng Kết Chánh Vụ Phong Ấp Hàng Tháng`:
  - Columns: `Thời Điểm`, `Phương Châm`, `Biến Động Dân & Khố`, `Binh Lực & Lương Thực`, `Huân Công`, `Tường Trình Nha Môn`.
  - Provides transparent, at-a-glance feedback on autonomous cabinet performance every month.

---

## 4. Verification & Invariants
- **Deterministic Simulation**: All random outcomes use kernel `IRng` or deterministic formulas.
- **Ledger Invariance**: Local fief treasury is isolated from the player's personal wallet, guaranteeing zero drift on Golden Replay hashes (`merchant-30d.json` balance 1514, `merchant-full.json` balance 778).
- **Test Coverage**: Verified in `DeepOfficialdomTests.cs` (716 total passing tests across the engine).
