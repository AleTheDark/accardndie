import {Scene} from 'phaser'

class Room extends Scene{

    constructor() {
        super("Room")
    }

    init(data){
        this.gm = data.gm
    }

    preload (){
        this.load.image('pvp1_bg','src/assets/pvp_bg.png')
        this.load.image('attack','src/assets/attack.png')
        this.load.image('shield','src/assets/shield.png')
        this.load.atlas('cards_m','src/assets/cards.png','src/assets/cards_m.json')
        this.load.atlas('retro','src/assets/cards.png','src/assets/retro.json')
    }

    create(){
        var gm = this.gm
        gm.room_type = 1;   
        this.dark = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1)
        this.lighter()
        this.setupScenario(gm)
    
    }

    handleFight(gm){
        this.screenX = 1280
        this.screenY = 760
        this.selected_card = null;
        this.imm_deckp1 = this.setupCards(0,gm.player1.deckp1)
        console.log(gm.player1.deckp1.length);
        this.imm_decke = this.setupCards(1,null)
        this.deduciStats(this.imm_deckp1)
        this.deduciStats(this.imm_decke)
        //enable interazioni pvp
        this.listenerPvp()
        
    }

    handleNpc(gm){}

    battle(a,d,imm_a,imm_d,decks_a,decks_d,liv_a,liv_d,enemy){

        if(d.getData('enemy') == 1 && a != null){
            
            this.selected_enemy = d
            this.offTouch()

            a.setData('state','fight')
            d.setData('state','fight')

            this.imm_attack.destroy()
            this.imm_shield.destroy()

            this.imm_attack = this.add.image(a.x+70,a.y+96,'attack').setDepth(2)
            this.imm_shield = this.add.image(d.x+70,d.y+96,'shield').setDepth(2)


            this.selected_card.setData('state','fight')
            this.selected_enemy.setData('state','fight')

            this.checkWinner(a,d,imm_a,imm_d,decks_a,decks_d,liv_a,liv_d,enemy)

            this.tweens.add({
                targets:this.imm_attack,
                x:this.screenX/2-120,
                y:this.screenY/2-30,
                duration:1000,
                ease: 'Power2',
                onComplete:()=>{
                    
                }
            });

            this.tweens.add({
                targets:this.imm_shield,
                x:this.screenX/2+60,
                y:this.screenY/2-30,
                duration:1000,
                ease: 'Power2',
                onComplete:()=>{
                  
                }

            })

        }

    }

    listenerPvp(){

        //Input click
        this.input.on('gameobjectdown', function (pointer, go) {
            console.log("Ho clickato "+go.getData('name'))
            this.focusCard(go)
            this.battle(this.selected_card,go,this.imm_deckp1,this.imm_decke,this.deckp1,this.decke,0,0,0)
        },this)
 
        //Input over
        this.input.on('gameobjectover',function(pointer,go){
            //console.log("nome "+go.getData('name'));
            //console.log("state "+go.getData('state'));
            //GESTIONE SPADE IN
            //Se il nemico sta in idle e lo punto, printo le spade sul nemico e lui va in focused
            if(go.getData('enemy') == 1 && go.getData('state') == "idle" && this.selected_card != null && this.imm_shield == null ){
                this.imm_shield = this.add.image(go.x+70,go.y+96,'shield').setDepth(2);
            }
            //GESTIONE SPADE OUT-IN
            //Se il nemico sta in idle e lo punto, printo le spade sul nemico e lui va in focused solo se prima non avevo printato già le spade
            else if(go.getData('enemy') == 1 && go.getData('state') == "idle" && this.selected_card != null && this.imm_shield != null){
                this.imm_shield.destroy();
                this.imm_shield = this.add.image(go.x+70,go.y+96,'shield').setDepth(2);
            }
        },this);

        //Input out
        this.input.on('gameobjectout',function(pointer,go){
            //Quando il mouse va fuori da un nemico elimina le spade
            go.getData('enemy')==1 && this.imm_shield && go.getData('state')=='idle'!=null?this.imm_shield.destroy():null;
        },this);
        
    }

    setupCards(enemy,names){
        //Enemy = 0 (player), = 1 (enemy)
        const enemyQuantity = 2
        const x = 400
        const y = enemy==0? 500:100
        var deckn = []
        var imm_deckn = []
        
        //GENERA I NOMI DELLE CARTE SE NON CI SONO 
        if(names == null){
            var frames_names = this.textures.get('cards_m').getFrameNames()
            for(var i=0; i<enemyQuantity; i++){
                var elem = Phaser.Utils.Array.RemoveRandomElement(frames_names)
                Phaser.Utils.Array.Add(deckn,elem)
            }
        }
        else{
            deckn = names
        }

        //GENERA EFFETTIVAMENTE LE CARTE RENDERIZZANDOLE 
        for(var i=0; i<deckn.length; i++){
            var img = this.add.image(x+i*100, y, 'cards_m', deckn[i]).setData({name:deckn[i],state:"idle",enemy:enemy,x:x+i*100,y:y}).setOrigin(0).setInteractive().setDepth(1)
            Phaser.Utils.Array.Add(imm_deckn,img)
        }
        return imm_deckn
    }

    focusCard(go){
        //Clicco sulla carta state:IDLE -> FOCUSED        
        if(go.getData('enemy') == 0 && go.getData('state') == "idle" && this.selected_card != null){
            this.imm_attack.destroy()
            this.imm_attack = this.add.image(go.getData('x')+70,go.getData('y')+96,'attack').setDepth(2)
            this.selected_card.setData('state','idle')
            go.setData('state','focus')
            this.selected_card = go
        }
        else if(go.getData('enemy') == 0 && go.getData('state') == "idle" && this.selected_card == null){
            this.imm_attack = this.add.image(go.getData('x')+70,go.getData('y')+96,'attack').setDepth(2)
            go.setData('state','focus')
            this.selected_card = go
        }

    }

    deduciStats(imgs){
        for(var i = 0;i<imgs.length;i++){
            var nome = imgs[i].getData('name')
            imgs[i].setData('value',parseInt(nome[0])+2)
            console.log(imgs[i]);
            switch(nome[1]){
                case 'd': imgs[i].setData('symbols','diamonds')
                break;
                case 'h': imgs[i].setData('symbols','hearts')
                break;
                case 'c': imgs[i].setData('symbols','clubs')
                break;
                case 's': imgs[i].setData('symbols','spades')
                break;
            }
            console.log(imgs[i].getData('symbols') + " " + imgs[i].getData('value'))
        }
    }

    checkWinner(a,d,imm_a,imm_d,decks_a,decks_d,liv_a,liv_d,enemy){
        var adv = this.checkAdvantage(a,d)
        adv == 1 ? console.log("VANTAGGIO") : adv == 0 ? console.log("PARI") : console.log("SVANTAGGIO")
        var atk = Phaser.Math.Between(1,3)+a.getData('value')
        var atk2 = Phaser.Math.Between(1,3)+a.getData('value')
        var def = Phaser.Math.Between(1,3)+d.getData('value')
        adv==1?atk=atk>atk2?atk:atk2:atk=atk<atk2?atk:atk2
        var win = atk>def?1:0

        if(win){ 
            this.p("Hai vinto")
            Phaser.Utils.Array.Remove(decks_d, d)
            Phaser.Utils.Array.Remove(imm_d, d)
            d.destroy()
            if(imm_d==0){
                //So finiti i nemici 
                //this.clearHandler(imm_a,names_a,this.gm)
            }
        }
        else{
            this.p("Hai perso")
            d.setData('state','idle');
        }
        a.setData('state','idle');
        a = null;
        d = null;

        this.turn = 0;

        this.imm_attack.destroy();
        this.imm_shield.destroy();
    
    }

    checkAdvantage(a,d){
        var a_s = a.getData('symbols')
        var d_s = d.getData('symbols')
        console.log(a_s +" "+ d_s)
        switch(a_s){
            case 'hearts': 
            if(d_s == 'diamonds') return 1
            if(d_s == 'spades') return -1
            return 0;
            case 'diamonds':
            if(d_s == 'clubs') return 1
            if(d_s == 'hearts') return -1
            return 0;
            case 'clubs':
            if(d_s == 'spades') return 1
            if(d_s == 'diamonds') return -1
            return 0;
            case 'spades':
            if(d_s == 'hearts') return 1
            if(d_s == 'clubs') return -1
            return 0
        }
    }

    darker(){
        this.tweens.add({
            targets: this.dark,
            alpha: { from: 1, to: 0 },
            duration:1000,
        })
    }

    lighter(){
        this.tweens.add({
            targets: this.dark,
            alpha: { from: 0, to: 1 },
            duration:1000,
        })
    }

    setupScenario(gm){

        switch(gm.room_type){
            case 1: this.add.image(1280/2,760/2,'pvp1_bg').setDepth(0);
                    this.handleFight(gm)
                break
            case 2: //this.add.image(1280/2,760/2,'pvp2_bg').setDepth(0);
                break
            case 3: //this.add.image(1280/2,760/2,'pvp3_bg').setDepth(0);
                break
            case 4: //this.add.image(1280/2,760/2,'pvp4_bg').setDepth(0);
                break
            case 5:
                break
            case 6:
                break
            case 7:
                break
            case 8:
                break
            default: this.p("erro")
        }
    }

    offTouch(){
        this.input.removeListener('gameobjectout')
        this.input.removeListener('gameobjectover')
        this.input.removeListener('gameobjectdown')
    }

    on(){

    }

    p(s){
        console.log(s)
    }

}

export default Room