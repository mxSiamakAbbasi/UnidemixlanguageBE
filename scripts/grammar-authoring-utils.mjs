import fs from "node:fs";
export const normalizationRules=["trim","collapseWhitespace","ignoreTerminalPunctuation","ignoreInitialCapitalization"];
export const exercise=(id,type,subskill,promptDe,options,acceptedAnswers,explanationFa,errorReason,contextDomain,semanticFamily)=>({id,type,subskill,promptFa:"دستور آلمانی را بخوانید و پاسخ مناسب را وارد یا انتخاب کنید.",promptDe,options,acceptedAnswers,explanationFa,qaStatus:"PASS",normalizationRules,errorReason,contextDomain,semanticFamily});
export function publishAuthoredTopic(file,topic){
 const pkg=JSON.parse(fs.readFileSync(file,"utf8"));let n=0;
 for(const q of topic.exercises){if(!Array.isArray(q.options)||!Array.isArray(q.acceptedAnswers))throw new Error(`Malformed authored exercise: ${q.id}`);if(!q.options.length)continue;const correct=q.options.find(x=>q.acceptedAnswers.includes(x));if(correct==null)throw new Error(`Correct option missing: ${q.id}`);const target=[1,2,0,2,1,0,1][n++%7];q.options=q.options.filter(x=>x!==correct);q.options.splice(target,0,correct)}
 const index=pkg.topics.findIndex(x=>x.contentKey===topic.contentKey);if(index>=0)pkg.topics[index]=topic;else pkg.topics.push(topic);pkg.topics.sort((a,b)=>a.order-b.order);fs.writeFileSync(file,JSON.stringify(pkg,null,2)+"\n");
}
