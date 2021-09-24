import {Scene} from 'phaser'

class Room extends Scene{

    constructor() {
        super("Room");
    }

    init(data){
        this.gameManager = data.gm;
    }
    preload (){
        this.load.image('pvp_bg','src/assets/pvp_bg.png');
        this.load.image('attack','src/assets/attack.png');
        this.load.image('shield','src/assets/shield.png');
        this.load.image('dogma','src/assets/dogma.png');
        this.load.atlas('cards_m','src/assets/cards.png','src/assets/cards_m.json');
        this.load.atlas('retro','src/assets/cards.png','src/assets/retro.json');
    }
    create(){
        var gameManager  = this.gameManager;
        var room_type = gameManager.room_type;
        var deckp1 = gameManager.player1.deckp1;
        var imm_deckp1 = [];
        console.log(gameManager.room_type+" è la porcamadonna");
        this.add.text(400,0,"Room_scene").setOrigin(0);
        console.log("diocane2 room numero " +room_type);
        ////////////////////////////////////////////////////////////////////////DEVO SCRIPTARE DELLE COSE COME IL MAZZO PER POTER PROGRAMMARE
        var frames_names_e = this.textures.get('cards_m').getFrameNames();
        var decke = [];
        var imm_decke = [];
        var stage = new Phaser.GameObjects.Container(this);
        var selected_card;
        var selected_enemy
        var allowed = 1;
        var room_type = 1; 
        var imm_attack,imm_shield,log,win;
        var x = 400;
        var y = 500;
        var turn = 0

        /*
        //GENERA I 3 NOMI DELLE CARTE PLAYER 
        for(var i = 0;i<3;++i){
            let elem = Phaser.Utils.Array.RemoveRandomElement(frames_names_m);
            Phaser.Utils.Array.Add(deckp1,elem);
        }
        //GENERA EFFETTIVAMENTE LE CARTE RENDERIZZANDOLE PLAYER
        for(var i = 0;i<deckp1.length;i++){
            var img = this.add.image(x+i*100, 500, 'cards_m', deckp1[i]).setData({name:deckp1[i],state:"idle",enemy:0,x:x+i*100,y:500}).setOrigin(0).setInteractive().setDepth(1);
            Phaser.Utils.Array.Add(imm_deckp1,img);
        }*/
        console.log("turn "+turn );
        //GENERA I 2 NOMI DELLE CARTE ENEMY 
        for(var i = 0;i<1;++i){
            let elem = Phaser.Utils.Array.RemoveRandomElement(frames_names_e);
            Phaser.Utils.Array.Add(decke,elem);
        }
        //GENERA EFFETTIVAMENTE LE CARTE RENDERIZZANDOLE ENEMY
        for(var i = 0;i<decke.length;i++){
            var img = this.add.image(x+i*100, 100, 'cards_m', decke[i]).setData({name:decke[i],state:"idle",enemy:1,x:x+i*100,y:100}).setOrigin(0).setInteractive().setDepth(1);
            Phaser.Utils.Array.Add(imm_decke,img);
        }
        for(var i = 0;i<deckp1.length;i++){
            var img = this.add.image(x+i*100, 500, 'cards_m', deckp1[i]).setData({name:deckp1[i],state:"idle",enemy:0,x:x+i*100,y:500}).setOrigin(0).setInteractive().setDepth(1);
            console.log(img);
            Phaser.Utils.Array.Add(imm_deckp1,img);
        }
        console.log(JSON.stringify(imm_deckp1));
        console.log("deck player "+deckp1);
        console.log("deck enemy "+decke);
        deduci(imm_deckp1);
        deduci(imm_decke);
        ////////////////////////////////////////////////////////////////////////FINE SCRIPTING PER PROGRAMMARE
        //Gestore click sui gameobject
        this.input.on('gameobjectdown', function (pointer, go) {
            if(turn == 1) return;
            console.log("Ho clickato "+go.getData('name'));
            if(allowed == 0) return;
            //Clicco sulla carta state:IDLE -> FOCUSED        
            if(go.getData('enemy') == 0 && go.getData('state') == "idle" && selected_card != null){//&& selected_card != gameObject && selected_card != null && gameObject.getData('enemy')==0){
                console.log("sel y "+selected_card.getData('y'));
                this.tweens.add({
                    targets: selected_card,
                    y: { value: selected_card.getData('y'), duration: 10,}
                });
                this.tweens.add({
                    targets: go,
                    y: { value: y-50, duration: 100, ease: 'Bounce.easeOut',}
                });
                selected_card.setData('state','idle');
                go.setData('state','focus');
                selected_card = go;
            }
            else if(go.getData('enemy') == 0 && go.getData('state') == "idle" && selected_card == null){
                console.log("b");
                this.tweens.add({
                    targets: go,
                    y: { value: y-50, duration: 100, ease: 'Bounce.easeOut',}
                });
                go.setData('state','focus');
                selected_card = go;
            }

            //Clicco su nemico e ho selezionato la carta
            if(go.getData('enemy') == 1 && selected_card != null){
                selected_enemy = go;
                selected_card.setData('state','fight');
                selected_enemy.setData('state','fight');
                console.log("c");
                //Qui parte il fight
                console.log("Let's fight tra " +selected_card.getData('name')+ " e " + selected_enemy.getData('name'));
                imm_attack.destroy();
                //Quando clicco sul nemico
                this.tweens.add({
                    targets: r,
                    alpha: { from: 0, to: 1},
                    duration:1000,
                    onComplete:()=>{
                        //Oscuro i nemici non selezionati
                        for(var i=0; i<imm_decke.length; i++){
                            if(imm_decke[i] != selected_enemy){
                                imm_decke[i].setVisible(false);
                            }
                        }
                        //Oscuro gli alleati non selezionati
                        for(var i=0; i<imm_deckp1.length; i++){
                            if(imm_deckp1[i] != selected_card){
                                imm_deckp1[i].setVisible(false);
                            }
                        }
                        //Porto il nemico in posizione
                        this.tweens.add({
                            targets: selected_enemy,
                            x:800,
                            y:285,
                            duration:1000,
                            ease:'Linear',
                            onComplete:()=>{
                                imm_shield = this.add.image(870,200,'shield');
                            }                                
                        });
                        //Porto l'alleato in posizione
                        this.tweens.add({
                            targets: selected_card,
                            x:320,
                            y:285,
                            duration:1000,
                            ease:'Linear',
                            onComplete:()=>{
                                imm_attack = this.add.image(390,200,'attack');
                                log = this.add.text(0, 0, "0");
                                log.text = " ";
                                log = this.add.text(200, 600, "Log combattimento:\n", { font: '"Press Start 2P"' });
                                //var dogma = this.add.image (640,190,'dogma');
                                var erne = checkAdvantage(selected_card,selected_enemy);
                                erne == 1 ? console.log("VANTAGGIO") : erne == 0 ? console.log("PARI") : console.log("SVANTAGGIO");
                                var atk = Phaser.Math.Between(1,3);
                                var atk2 = Phaser.Math.Between(1,3);
                                var def = Phaser.Math.Between(1,3);
                                erne==1?atk=atk>atk2?atk:atk2:atk=atk<atk2?atk:atk2;
                                //Hai distrutto l'avversario
                                atk += selected_card.getData('value');
                                atk2 += selected_card.getData('value');
                                def += selected_enemy.getData('value');
                                //console.log("Attacco finale "+atk);
                                //console.log("Difesa finale "+def);
                                //TODO Sistemare log
                                var win = atk>def?1:0;
                                if(win){
                                    //console.log("Vinto");
                                    log.text += "Hai vinto, "
                                    log.text += erne==1?("con vantaggio\n"):("con svantaggio\n");
                                    log.text += "Hai rollato "+atk+" "
                                    log.text += erne==1?(" e "+atk2+" con vantaggio"):(" e "+atk2+" con svantaggio");
                                    Phaser.Utils.Array.Remove(decke, selected_enemy);
                                    Phaser.Utils.Array.Remove(imm_decke, selected_enemy);

                                    selected_card.setData('state','idle');
                                    this.tweens.add({
                                        targets:selected_card,
                                        x:selected_card.getData('x'),
                                        y:selected_card.getData('y'),
                                        duration:1000,
                                        ease: 'Linear',
                                    });
                                    this.tweens.add({
                                        targets:selected_enemy,
                                        alpha:{from:1,to:0},
                                        duration:1000,
                                        ease: 'Power2',
                                        onComplete:()=>{selected_enemy.destroy();selected_enemy = null;
                                            if(imm_decke==0){   
                                                stage.on('cleared', this.clearHandler, this);
                                                stage.emit('cleared',imm_deckp1,deckp1,gameManager,allowed,turn);
                                            }
                                        }
                                    });
                                    selected_card = null;
                                }
                                else{
                                    log.text += "Non sei riuscito a vincere, ";
                                    log.text += erne==1?("con vantaggio\n"):("con svantaggio\n");
                                    log.text += "Hai provato infliggendo "+atk+" ";
                                    log.text += erne==1?(" e "+atk2+" con vantaggio"):(" e "+atk2+" con svantaggio");
                                    selected_card.setData('state','idle');
                                    selected_enemy.setData('state','idle');
                                    turn = 1;
                                    this.tweens.add({
                                        targets:selected_enemy,
                                        x:selected_enemy.getData('x'),
                                        y:selected_enemy.getData('y'),
                                        duration:1000,
                                        ease: 'Linear'
                                    });
                                    this.tweens.add({
                                        targets:selected_card,
                                        x:selected_card.getData('x'),
                                        y:selected_card.getData('y'),
                                        duration:1000,
                                        ease: 'Linear',
                                        onComplete:()=>{
                                            console.log("Dovrebbe mandare enemy attack");
                                            stage.on('enemyAttack',this.purpet,this);
                                            //stage.emit('enemyAttack',deckp1,imm_deckp1,decke,imm_decke);
                                            stage.emit('enemyAttack',deckp1,imm_deckp1,decke,imm_decke,turn);
                                        }
                                    });
                                    selected_enemy = null;
                                    selected_card = null;
                                }
                                this.tweens.add({
                                    targets:r,
                                    alpha:{from:1,to:0},
                                    duration:1000,
                                    ease: 'Linear',
                                });
                                //Back to the normal
                                for(var i=0; i<imm_deckp1.length; i++){imm_deckp1[i].setVisible(true);}
                                for(var i=0; i<imm_decke.length; i++){imm_decke[i].setVisible(true);}
                                imm_attack.destroy();
                                imm_shield.destroy();
                                log.destroy();
                            }
                        });    
                    }
                });
            }
        }, this);
        //GESTIONE SPADE 
        this.input.on('gameobjectover',function(pointer,go){
            console.log("nome "+go.getData('name'));
            console.log("state "+go.getData('state'));
            //GESTIONE SPADE IN
            //Se il nemico sta in idle e lo punto, printo le spade sul nemico e lui va in focused
            if(go.getData('enemy') == 1 && go.getData('state') == "idle" && selected_card != null && imm_attack == null ){
                imm_attack = this.add.image(go.x+70,go.y+96,'attack').setDepth(2);
            }
            //GESTIONE SPADE OUT-IN
            //Se il nemico sta in idle e lo punto, printo le spade sul nemico e lui va in focused solo se prima non avevo printato già le spade
            else if(go.getData('enemy') == 1 && go.getData('state') == "idle" && selected_card != null && imm_attack != null){
                imm_attack.destroy();
                imm_attack = this.add.image(go.x+70,go.y+96,'attack').setDepth(2);
            }
        },this);
        //GESTIONE SPADE OUT 
        this.input.on('gameobjectout',function(pointer,go){
            //Quando il mouse va fuori da un nemico elimina le spade
            go.getData('enemy')==1 && imm_attack!=null?imm_attack.destroy():null;
        },this);

        stage.on('deduciStanza', this.deduciStanza, this);
        stage.emit('deduciStanza',room_type);

        var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
        this.tweens.add({
            targets: r,
            alpha: { from: 1, to: 0 },
            duration:1000,
        });
    }
    upload(){}

    purpet(deckp1,imm_deckp1,decke,imm_decke,turn){
        var imm_attack,imm_shield,log,win;
        console.log("turno :"+turn);
        //Attacca il nemico
        console.log(imm_decke);
        var selected_enemy = imm_decke[Phaser.Math.Between(0,(decke.length)-1)];
        var selected_card = imm_deckp1[Phaser.Math.Between(0,(deckp1.length)-1)];
        console.log("Let's fight tra " +selected_card.getData('name')+ " e " + selected_enemy.getData('name'));
        //Qui parte il fight
        //console.log("Let's fight tra " +selected_card.getData('name')+ " e " + selected_enemy.getData('name'));
        var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
        this.tweens.add({
            targets: r,
            alpha: { from: 0, to: 1},
            duration:1000,
            onComplete:()=>{
                //Oscuro i nemici non selezionati
                for(var i=0; i<imm_decke.length; i++){
                    if(imm_decke[i] != selected_enemy){
                        imm_decke[i].setVisible(false);
                    }
                }
                //Oscuro gli alleati non selezionati
                for(var i=0; i<imm_deckp1.length; i++){
                    if(imm_deckp1[i] != selected_card){
                        imm_deckp1[i].setVisible(false);
                    }
                }
                //Porto il nemico in posizione
                this.tweens.add({
                    targets: selected_enemy,
                    x:320,
                    y:285,
                    duration:1000,
                    ease:'Linear',
                    onComplete:()=>{
                        imm_shield = this.add.image(870,200,'shield');
                    }                                
                });
                //Porto l'alleato in posizione
                this.tweens.add({
                    targets: selected_card,
                    x:800,
                    y:285,
                    duration:1000,
                    ease:'Linear',
                    onComplete:()=>{
                        imm_attack = this.add.image(390,200,'attack');
                        log = this.add.text(0, 0, "0");
                        log.text = " ";
                        log = this.add.text(200, 600, "Log combattimento:\n", { font: '"Press Start 2P"' });
                        var erne = checkAdvantage(selected_enemy,selected_card);
                        erne == 1 ? console.log("VANTAGGIO") : erne == 0 ? console.log("PARI") : console.log("SVANTAGGIO");
                        var atk = Phaser.Math.Between(1,3);
                        var atk2 = Phaser.Math.Between(1,3);
                        var def = Phaser.Math.Between(1,3);
                        erne==1?atk=atk>atk2?atk:atk2:atk=atk<atk2?atk:atk2;
                        //Hai distrutto l'avversario
                        atk += selected_card.getData('value');
                        atk2 += selected_card.getData('value');
                        def += selected_enemy.getData('value');
                        //console.log("Attacco finale "+atk);
                        //console.log("Difesa finale "+def);
                        //TODO Sistemare log
                        var win = atk>def?1:0;
                        if(win){
                            //console.log("Vinto");
                            log.text += "Hai perso, "
                            log.text += erne==1?("con vantaggio\n"):("con svantaggio\n");
                            log.text += "Ha rollato "+atk+" "
                            log.text += erne==1?(" e "+atk2+" con vantaggio"):(" e "+atk2+" con svantaggio");
                            Phaser.Utils.Array.Remove(deckp1, selected_card);
                            Phaser.Utils.Array.Remove(imm_deckp1, selected_card);

                            selected_enemy.setData('state','idle');
                            this.tweens.add({
                                targets:selected_enemy,
                                x:selected_enemy.getData('x'),
                                y:selected_enemy.getData('y'),
                                duration:1000,
                                ease: 'Linear',
                            });
                            this.tweens.add({
                                targets:selected_card,
                                alpha:{from:1,to:0},
                                duration:1000,
                                ease: 'Power2',
                                onComplete:()=>{selected_card.destroy();selected_card = null;
                                    if(imm_deckp1==0){
                                        //Se non ho piu carte TODO   
                                        turn = 0;
                                        stage.on('wiped', this.wipedHandler, this);
                                        stage.emit('wiped',imm_deckp1,deckp1,gameManager,allowed);
                                    }
                                }
                            });
                            selected_enemy = null;
                        }
                        else{
                            log.text += "Sei riuscito a farla franca xd, ";
                            log.text += erne==1?("con vantaggio\n"):("con svantaggio\n");
                            log.text += "Hai provato infliggendoti "+atk+" ";
                            log.text += erne==1?(" e "+atk2+" con vantaggio"):(" e "+atk2+" con svantaggio");
                            selected_card.setData('state','idle');
                            selected_enemy.setData('state','idle');
                            this.tweens.add({
                                targets:selected_enemy,
                                x:selected_enemy.getData('x'),
                                y:selected_enemy.getData('y'),
                                duration:1000,
                                ease: 'Linear'
                            });
                            this.tweens.add({
                                targets:selected_card,
                                x:selected_card.getData('x'),
                                y:selected_card.getData('y'),
                                duration:1000,
                                ease: 'Linear'
                            });
                            selected_enemy = null;
                            selected_card = null;
                        }
                        this.tweens.add({
                            targets:r,
                            alpha:{from:1,to:0},
                            duration:1000,
                            ease: 'Linear'
                        });
                        //Back to the normal
                        for(var i=0; i<imm_deckp1.length; i++){imm_deckp1[i].setVisible(true);}
                        for(var i=0; i<imm_decke.length; i++){imm_decke[i].setVisible(true);}
                        imm_attack.destroy();
                        imm_shield.destroy();
                        log.destroy();
                        turn = 0;
                    }
                });    
            } 
        })
    }

    clearHandler(imm_deckp1,deckp1,gm,allowed){
        //Vedi se ci sono ancora nemici
        console.log("So finiti i mob, tocca tirà dritti");
        for(var i = 0;i<imm_deckp1.length;i++){ 
            this.tweens.add({
                targets: imm_deckp1[i],
                x: 530,
                y: 100,
                delay:100, //100+i*600 effetto uno per uno
                duration:600,
                ease: 'Linear',
                onComplete:()=>{
                    allowed = 0;
                    var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
                    this.tweens.add({
                        targets: r,
                        alpha: { from: 0, to: 1 },
                        duration:1000,
                        onComplete:() =>{   
                            //Incrementa gestione stage exp etc.     
                            gm.player1.deckp1 = deckp1;                 
                            this.scene.start("Choose",{ gm: gm});
                        }
                    });
                }
            });
        }
    }

    wipedHandler(){}

    deduciStanza(room_type){
        switch(room_type){
            case 1: this.add.image(1280/2,760/2,'pvp_bg').setDepth(0);
            break;
            case 2: 
            break;
            case 3:
            break;
            case 4:
            break;
            case 5: this.add.image(1280/2,760/2,'pvp_bg');  
            break;
            default:console.log("Room_type "+room_type);
        }
    }
}

function checkAdvantage(a,d){
    var a_s = a.getData('symbols');
    var d_s = d.getData('symbols');
    console.log(a_s +" "+ d_s);
    switch(a_s){
        case 'hearts': 
        if(d_s == 'diamonds') return 1;
        if(d_s == 'spades') return -1;
        return 0;
        case 'diamonds':
        if(d_s == 'clubs') return 1;
        if(d_s == 'hearts') return -1;
        return 0;
        case 'clubs':
        if(d_s == 'spades') return 1;
        if(d_s == 'diamonds') return -1;
        return 0;
        case 'spades':
        if(d_s == 'hearts') return 1;
        if(d_s == 'clubs') return -1;
        return 0;
    }
}

function deduci(imgs){
    for(var i = 0;i<imgs.length;i++){
        var nome = imgs[i].getData('name');
        imgs[i].setData('value',parseInt(nome[0])+2);
        console.log(imgs[i]);
        switch(nome[1]){
            case 'd': imgs[i].setData('symbols','diamonds');
            break;
            case 'h': imgs[i].setData('symbols','hearts');
            break;
            case 'c': imgs[i].setData('symbols','clubs');
            break;
            case 's': imgs[i].setData('symbols','spades');
            break;
        }
        console.log(imgs[i].getData('symbols') + " " + imgs[i].getData('value'));
    }
}
    
export default Room