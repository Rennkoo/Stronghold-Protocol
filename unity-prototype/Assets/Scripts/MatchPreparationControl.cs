using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public class MatchPiece {public int uid,idx,slot,price,row,col;public string name,kind;public bool sold,golden;}
[Serializable] public class MatchTile {public int row,col;public string kind;}
[Serializable] public class MatchOption {public string id,name,desc;}
[Serializable] public class MatchShop {public int level,refreshPrice,upgradePrice;public bool frozen;public MatchPiece[] slots;}
[Serializable] public class MatchUi {public string phase,stageId;public int round,lp,funds,deployCount,deployCap;public bool canReady,ready,ended,paused;public MatchShop shop;public MatchPiece[] hand,temp,board;public MatchTile[] tiles;public MatchOption[] bands,cards,effects;}
[Serializable] public class MatchReply {public string error,detail;}
[Serializable] public class MatchEnvelope {public MatchUi ui;public MatchReply reply;}

// First interactive full-match adapter. Scene artwork and battle rendering remain a separate migration step.
public sealed class MatchPreparationControl:MonoBehaviour {
    MatchUi state;bool busy;float nextPoll;int selected;string message="连接本地规则引擎…",direction="RIGHT";Font font;Vector2 scroll;
    bool smoke,smokeBought,smokeDone;
    void Start(){Application.runInBackground=true;Application.targetFrameRate=60;smoke=Array.IndexOf(Environment.GetCommandLineArgs(),"-match-smoke")>=0;font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",16);StartCoroutine(Send("{\"action\":\"new\"}"));}
    void Update(){
        if(busy||state==null)return;
        if(smoke&&!smokeDone){
            if(state.phase=="INFO_CHECK"){Action("g.infoReady");return;}
            if(state.phase=="BAND_DRAFT"){Intent("\"t\":\"g.band\",\"bandId\":\""+state.bands[0].id+"\"");return;}
            if(state.phase=="PREP"){
                if(!smokeBought){foreach(var p in state.shop.slots)if(p!=null&&!p.sold&&p.price<=state.funds){smokeBought=true;Intent("\"t\":\"g.buy\",\"slot\":"+p.slot);return;}}
                else if(Array.Exists(state.hand,p=>p!=null)){smokeDone=true;Debug.Log("UNITY MATCH SMOKE PASS: draft, shop and hand synchronized");}
            }
        }
        if(!state.ended&&Time.unscaledTime>=nextPoll)StartCoroutine(Send("{\"action\":\"step\"}"));
    }
    IEnumerator Send(string json){
        busy=true;
        using(var request=new UnityWebRequest("http://127.0.0.1:3142/match","POST")){
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=10;
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)message="连接失败："+request.downloadHandler.text;
            else {var response=JsonUtility.FromJson<MatchEnvelope>(request.downloadHandler.text);state=response.ui;message=string.IsNullOrEmpty(response.reply.error)?"操作已同步":response.reply.error+" "+response.reply.detail;}
        }
        busy=false;nextPoll=Time.unscaledTime+.05f;
    }
    void Intent(string body){if(!busy)StartCoroutine(Send("{\"action\":\"intent\",\"intent\":{"+body+"}}"));}
    void Action(string t){Intent("\"t\":\""+t+"\"");}
    bool Button(string text){return GUILayout.Button(text,GUILayout.MinHeight(32));}
    string Name(MatchPiece p){return p.name+(p.golden?" [精锐]":"");}
    void OnGUI(){
        if(font)GUI.skin.font=font;
        GUI.skin.label.wordWrap=true;
        GUILayout.BeginArea(new Rect(20,20,Screen.width-40,Screen.height-40));
        GUILayout.Label("卫戍协议 · 完整规则接入测试（界面与战斗美术仍在移植）");
        GUILayout.Label(message);if(state==null){GUILayout.EndArea();return;}
        GUILayout.Label($"{state.phase}  回合 {state.round}  生命 {state.lp}  资金 {state.funds}  部署 {state.deployCount}/{state.deployCap}");
        GUI.enabled=!busy;
        if(state.phase=="INFO_CHECK"){if(Button("确认战前情报"))Action("g.infoReady");}
        else if(state.phase=="BAND_DRAFT"){
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(var band in state.bands){if(Button(band.name+"  "+band.desc))Intent("\"t\":\"g.band\",\"bandId\":\""+band.id+"\"");}
            GUILayout.EndScrollView();
        }else if(state.phase=="SP_DRAFT"){
            for(int i=0;i<state.cards.Length;i++)if(Button(state.cards[i].name+"  "+state.cards[i].desc))Intent("\"t\":\"g.choice\",\"idx\":"+i);
        }else if(state.phase=="PREP")DrawPreparation();
        else GUILayout.Label(state.ended?"本局结束，可重新开局。":"原版引擎正在推进战斗与结算；本界面尚未显示战斗模型。");
        GUILayout.BeginHorizontal();
        if(Button("AI 托管"))Intent("\"t\":\"g.autoplay\",\"on\":true");
        if(Button("收回托管"))Intent("\"t\":\"g.autoplay\",\"on\":false");
        if(Button(state.paused?"继续":"暂停"))Intent("\"t\":\"g.pause\",\"on\":"+(!state.paused?"true":"false"));
        if(Button("重新开局")){selected=0;StartCoroutine(Send("{\"action\":\"new\"}"));}
        GUILayout.EndHorizontal();GUI.enabled=true;GUILayout.EndArea();
    }
    void DrawPreparation(){
        GUILayout.BeginHorizontal();
        GUILayout.Label("调度中心 Lv."+state.shop.level);
        if(Button("刷新 / "+state.shop.refreshPrice))Action("g.refresh");
        if(Button("升级 / "+state.shop.upgradePrice))Action("g.levelUp");
        if(Button(state.shop.frozen?"解冻":"冻结"))Action("g.freeze");
        if(Button("完成准备"))Intent("\"t\":\"g.ready\",\"ready\":true");
        GUILayout.EndHorizontal();GUILayout.BeginHorizontal();
        foreach(var p in state.shop.slots)if(p!=null){GUI.enabled=!busy&&!p.sold;if(Button(Name(p)+" / "+p.price))Intent("\"t\":\"g.buy\",\"slot\":"+p.slot);}
        GUI.enabled=!busy;GUILayout.EndHorizontal();
        GUILayout.Label("选择手牌或场上单位，再点击地块部署；点击空手牌位可撤回。方向："+direction);
        GUILayout.BeginHorizontal();foreach(string dir in new[]{"RIGHT","UP","LEFT","DOWN"})if(Button(dir))direction=dir;
        if(selected!=0&&Button("出售选中单位"))Intent("\"t\":\"g.sell\",\"uid\":"+selected);GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        for(int i=0;i<state.hand.Length;i++){var p=state.hand[i];if(Button(p==null?"空位":Name(p))){if(p!=null)selected=p.uid;else if(selected!=0)Intent("\"t\":\"g.move\",\"uid\":"+selected+",\"to\":{\"area\":\"hand\",\"idx\":"+i+"}");}}
        GUILayout.EndHorizontal();
        if(state.temp!=null)foreach(var p in state.temp)if(p!=null&&Button("临时区："+Name(p)))selected=p.uid;
        for(int row=9;row<=12;row++){
            GUILayout.BeginHorizontal();
            for(int col=2;col<=10;col++){
                var piece=Array.Find(state.board,p=>p.row==row&&p.col==col);var tile=Array.Find(state.tiles,t=>t.row==row&&t.col==col);
                GUI.enabled=!busy&&tile!=null;
                if(Button(piece!=null?Name(piece):tile==null?"不可部署":tile.kind=="melee"?"地面":"高台")){
                    if(selected==0&&piece!=null)selected=piece.uid;
                    else if(selected!=0)Intent("\"t\":\"g.move\",\"uid\":"+selected+",\"dir\":\""+direction+"\",\"to\":{\"area\":\"board\",\"row\":"+row+",\"col\":"+col+"}");
                }
            }GUILayout.EndHorizontal();
        }GUI.enabled=!busy;
    }
}
