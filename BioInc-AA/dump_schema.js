const Database = require('better-sqlite3');
const base = 'C:/Program Files (x86)/Steam/steamapps/common/Bio Inc. Redemption/BioIncRedemption_Data/StreamingAssets/';
for (const db of ['BioInc.db', 'BioInc_Symptoms.db']) {
  console.log('========== ' + db + ' ==========');
  const con = new Database(base + db, { readonly: true });
  for (const t of con.prepare("select name,sql from sqlite_master where type='table'").all()) {
    console.log('-- table ' + t.name);
    console.log(t.sql);
    try {
      const n = con.prepare('select count(*) c from "' + t.name + '"').get().c;
      console.log('   rows: ' + n);
      const cols = con.prepare('pragma table_info("' + t.name + '")').all().map(c => c.name);
      console.log('   cols: ' + cols.join(', '));
      const sample = con.prepare('select * from "' + t.name + '" limit 2').all();
      console.log('   sample: ' + JSON.stringify(sample).slice(0, 800));
    } catch (e) { console.log('   err ' + e.message); }
  }
  con.close();
}
