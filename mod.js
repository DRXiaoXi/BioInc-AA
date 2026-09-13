#!/usr/bin/env node
/**
 * Bio Inc. Redemption 数据库MOD工具
 *
 * 游戏在启动时以读写方式打开
 *   <游戏目录>/BioIncRedemption_Data/StreamingAssets/BioInc.db   (主数据: 疾病/风险/治疗/费用)
 *   <游戏目录>/BioIncRedemption_Data/StreamingAssets/BioInc_Symptoms.db (诊断症状数据)
 * 两者均为未加密 SQLite。直接修改即可生效，无需改动程序集。
 *
 * 用法:
 *   node mod.js backup                     备份原始数据库到 mods/backup/
 *   node mod.js restore                    从备份还原
 *   node mod.js list [关键词]              列出健康条件(疾病/风险/加成器等)
 *   node mod.js list-symptoms              列出症状表
 *   node mod.js info <Id或名称>            查看某个健康条件详情(含治疗/费用/解剖点)
 *   node mod.js set <Id> <字段=值> ...     修改健康条件字段 (Cost, RepeatCount, UpdateInterval, Prerequiste, ...)
 *   node mod.js treatments <Id>            查看某条件的治疗方案
 *   node mod.js set-treatment <rowId> <字段=值> ...   修改治疗 (TreatmentTime, Efficiency, Cost)
 *   node mod.js anatomy <Id> <system=值> ...          修改每次复发对各系统的伤害
 *      system ∈ circulatory,digestive,immune,muscular,nervous,respiratory,skeletal,renal (前缀 Repeat*)
 *   node mod.js patch <mods/xxx.json>      应用JSON补丁包(见 mods/examples)
 *   node mod.js global <名称=数值> ...     全局修改: e.g. disease-cost=3 表示所有 Disease 型 Cost<=3
 */
const fs = require('fs');
const path = require('path');
const Database = require('better-sqlite3');

const GAME = 'C:/Program Files (x86)/Steam/steamapps/common/Bio Inc. Redemption/BioIncRedemption_Data/StreamingAssets';
const DB_MAIN = path.join(GAME, 'BioInc.db');
const DB_SYM = path.join(GAME, 'BioInc_Symptoms.db');
const HERE = __dirname;
const BACKUP_DIR = path.join(HERE, 'mods', 'backup');

function open(p) { return new Database(p); }
function ensureBackup() {
  fs.mkdirSync(BACKUP_DIR, { recursive: true });
  for (const f of ['BioInc.db', 'BioInc_Symptoms.db'])
    if (!fs.existsSync(path.join(BACKUP_DIR, f)))
      fs.copyFileSync(path.join(GAME, f), path.join(BACKUP_DIR, f));
}
function kvPairs(args) {
  const out = {};
  for (const a of args) {
    const i = a.indexOf('=');
    if (i < 0) throw new Error('参数应为 字段=值: ' + a);
    const k = a.slice(0, i), v = a.slice(i + 1);
    out[k] = /^-?\d+(\.\d+)?$/.test(v) ? Number(v) : v;
  }
  return out;
}
function findCondition(db, key) {
  if (/^\d+$/.test(key)) return db.prepare('select * from HealthConditionData where Id=?').get(Number(key));
  return db.prepare('select * from HealthConditionData where Name like ?').all('%' + key + '%');
}

const cmd = process.argv[2];
const args = process.argv.slice(3);
try {
  switch (cmd) {
    case 'backup': ensureBackup(); console.log('已备份到 ' + BACKUP_DIR); break;
    case 'restore':
      for (const f of ['BioInc.db', 'BioInc_Symptoms.db']) fs.copyFileSync(path.join(BACKUP_DIR, f), path.join(GAME, f));
      console.log('已还原。');
      break;
    case 'list': {
      const db = open(DB_MAIN, { readonly: true });
      const kw = args[0] ? '%' + args[0] + '%' : '%';
      const rows = db.prepare('select Id,Name,Type,Cost,RepeatCount,UpdateInterval,Prerequiste from HealthConditionData where Name like ? order by Type,Id').all(kw);
      for (const r of rows) console.log(`${String(r.Id).padStart(4)}  T${r.Type}  cost=${String(r.Cost).padStart(3)}  rep=${String(r.RepeatCount).padStart(3)}  int=${String(r.UpdateInterval).padStart(6)}  ${r.Name}${r.Prerequiste ? '  (前置:' + r.Prerequiste + ')' : ''}`);
      console.log(rows.length + ' 条');
      break;
    }
    case 'list-symptoms': {
      const db = open(DB_SYM, { readonly: true });
      for (const r of db.prepare('select * from SymptomData order by Id').all()) console.log(r.Id + '\t' + r.Name);
      break;
    }
    case 'info': {
      const db = open(DB_MAIN, { readonly: true });
      const rows = findCondition(db, args[0]);
      for (const c of [].concat(rows)) {
        console.log(JSON.stringify(c, null, 2));
        console.log(' 解剖点:', JSON.stringify(db.prepare('select * from AnatomyPointsData where Id=?').get(c.Id)));
        console.log(' 症状:', JSON.stringify(db.prepare('select * from HealthConditionSymptomsData where HealthConditionId=?').all(c.Id)));
        console.log(' 治疗:', JSON.stringify(db.prepare('select * from TreatmentData where HealthConditionId=?').all(c.Id)));
      }
      break;
    }
    case 'treatments': {
      const db = open(DB_MAIN, { readonly: true });
      const rows = findCondition(db, args[0]);
      for (const c of [].concat(rows)) {
        for (const t of db.prepare('select * from TreatmentData where HealthConditionId=?').all(c.Id))
          console.log(`row=${t.Id}  cond=${c.Name}  TreatmentId=${t.TreatmentId}  time=${t.TreatmentTime}ms  eff=${t.Efficiency}%  cost=${t.Cost}`);
      }
      break;
    }
    case 'set': {
      ensureBackup();
      const db = open(DB_MAIN);
      const upd = kvPairs(args.slice(1));
      const cols = Object.keys(upd).map(k => '`' + k + '`=@' + k).join(',');
      for (const key of [args[0]].flat()) {
        const rows = [].concat(findCondition(db, key));
        if (typeof rows[0] === 'undefined') throw new Error('未找到: ' + key);
        for (const r of rows) {
          db.prepare('update HealthConditionData set ' + cols + ' where Id=' + r.Id).run(upd);
          console.log('已更新', r.Id, r.Name);
        }
      }
      break;
    }
    case 'set-treatment': {
      ensureBackup();
      const db = open(DB_MAIN);
      const upd = kvPairs(args.slice(1));
      const cols = Object.keys(upd).map(k => '`' + k + '`=@' + k).join(',');
      db.prepare('update TreatmentData set ' + cols + ' where Id=' + Number(args[0])).run(upd);
      console.log('已更新治疗行 ' + args[0]);
      break;
    }
    case 'anatomy': {
      ensureBackup();
      const db = open(DB_MAIN);
      const upd = kvPairs(args.slice(1));
      const map = { circulatory: 'Circulatory', digestive: 'Digestive', immune: 'Immune', muscular: 'Muscular', nervous: 'Nervous', respiratory: 'Respiratory', skeletal: 'Skeletal', renal: 'Renal' };
      const upd2 = {};
      for (const [k, v] of Object.entries(upd)) {
        const sys = map[k.toLowerCase().replace('repeat', '')];
        if (!sys) throw new Error('未知系统: ' + k);
        upd2['Repeat' + sys] = v;
      }
      const cols = Object.keys(upd2).map(k => '`' + k + '`=@' + k).join(',');
      db.prepare('update AnatomyPointsData set ' + cols + ' where Id=' + Number(args[0])).run(upd2);
      console.log('已更新解剖点 ' + args[0]);
      break;
    }
    case 'global': {
      ensureBackup();
      const db = open(DB_MAIN);
      const upd = kvPairs(args);
      for (const [k, v] of Object.entries(upd)) {
        if (k === 'disease-cost') {
          const n = db.prepare('update HealthConditionData set Cost=? where Type=1 and Cost>?').run(v, v);
          console.log('疾病费用>' + v + ' → ' + v + ': ' + n.changes + ' 条');
        } else if (k === 'treatment-cost') {
          const n = db.prepare('update TreatmentData set Cost=? where Cost>?').run(v, v);
          console.log('治疗费用>' + v + ' → ' + v + ': ' + n.changes + ' 行');
        } else if (k === 'treatment-time') {
          const n = db.prepare('update TreatmentData set TreatmentTime=? where TreatmentTime>?').run(v, v);
          console.log('治疗时间>' + v + 'ms → ' + v + 'ms: ' + n.changes + ' 行');
        } else if (k === 'clear-prereq') {
          const n = db.prepare("update HealthConditionData set Prerequiste='' where Prerequiste<>''").run();
          console.log('清除前置条件: ' + n.changes + ' 条');
        } else throw new Error('未知全局项: ' + k);
      }
      break;
    }
    case 'patch': {
      ensureBackup();
      const pack = JSON.parse(fs.readFileSync(args[0], 'utf8'));
      const db = open(DB_MAIN);
      const trans = db.transaction(() => {
        for (const [key, fields] of Object.entries(pack.conditions || {})) {
          const cols = Object.keys(fields).map(k => '`' + k + '`=@' + k).join(',');
          for (const r of [].concat(findCondition(db, key))) {
            db.prepare('update HealthConditionData set ' + cols + ' where Id=' + r.Id).run(fields);
            console.log('条件', r.Id, r.Name);
          }
        }
        for (const [rowId, fields] of Object.entries(pack.treatments || {})) {
          const cols = Object.keys(fields).map(k => '`' + k + '`=@' + k).join(',');
          db.prepare('update TreatmentData set ' + cols + ' where Id=' + rowId).run(fields);
          console.log('治疗行', rowId);
        }
        for (const [id, fields] of Object.entries(pack.anatomy || {})) {
          const cols = Object.keys(fields).map(k => '`' + k + '`=@' + k).join(',');
          db.prepare('update AnatomyPointsData set ' + cols + ' where Id=' + id).run(fields);
          console.log('解剖点', id);
        }
      });
      trans();
      console.log('补丁应用完成: ' + args[0]);
      break;
    }
    default:
      console.log(fs.readFileSync(__filename, 'utf8').split('*/')[0].split('/**')[1]);
  }
} catch (e) { console.error('错误: ' + e.message); process.exit(1); }
