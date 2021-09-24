import {Scene} from 'phaser'


class Select extends Scene{
    
    constructor() {
        super("Select");
    }

    preload(){
        this.load.atlas('cards_m','src/assets/cards.png','src/assets/cards_m.json');
        this.load.atlas('cards_c','src/assets/cards.png','src/assets/cards_c.json');
        this.load.atlas('retro','src/assets/cards.png','src/assets/retro.json');
        this.load.image('select_bg','src/assets/select_bg.png');
        this.load.image('scarta_button','src/assets/scarta_button.png');
    }

    create(){
        //Legenda monsters    -format[h,d,c,s] lungo 36 da 0-8 ho [2:10][h] da 9-17[2:10][d] da 18-26[2:10][c] da 27-35[2:10][s]
        //Legenda consumables -format[h,d,c,s] lungo 20 da 0-3 ho [ajqk][h] da 4-7[ajqk][d] da 8-11[ajqk][c] 12-15[ajqk][s] 16-18[jolly][red] 19-20[jolly][black]
        this.add.text(400,0,"Select_scene").setOrigin(0);
        this.add.image(1280/2,760/2,'select_bg');
        var frames_names_m = this.textures.get('cards_m').getFrameNames();
        var frames_names_r = this.textures.get('retro').getFrameNames();

        var gameManager = {
            n_player:1,
            iniziative:{},
            room_type:0,
            exp_party:0,
            exp_room:0,
            stage:0,
            decke:null,
            imm_decke:null,
            player1:{
                deckp1:null
            },
            player2:{
                deckp2:null
            },
            player3:{
                deckp3:null
            }
        };

        /*for (var key in ) {
            console.log("key " + key + " has value " + [key]);
        }*/
        //console.log(JSON.stringify());
        //console.log(JSON.stringify()["exp_party"]);
        
        var selectedCard;
        let scarta_text ,si_text,no_text;
        const MAX_HAND_PLAYER = 5; 
        var deckp1 = [];
        var imm_deckp1 = [];
        var allowed = 1;
        var x = 400;
        var y = 500;
      
        var retro = this.add.image(1090,0,'retro',frames_names_r).setOrigin(0).setDepth(1);

        //<-----------------------------------GENERA 5 I NOMI DELLE CARTE  ------------------------------------->
        for(var i = 0;i<MAX_HAND_PLAYER;++i){
            let elem = Phaser.Utils.Array.RemoveRandomElement(frames_names_m);
            Phaser.Utils.Array.Add(deckp1,elem);
        }
        //<-----------------------------------GENERA EFFETTIVAMENTE LE CARTE RENDERIZZANDOLE ------------------------------------->
        for(var i = 0;i<deckp1.length;i++){
            var img = this.add.image(1090, 0, 'cards_m', deckp1[i]).setData({name:deckp1[i],state:"idle",value:null,symbol:null}).setOrigin(0).setInteractive();
            Phaser.Utils.Array.Add(imm_deckp1,img);
        }
        //<-----------------------------------ANIMAZIONE CARTE DAL MAZZO ALLA MANO ------------------------------------->
        for(var i = 0;i<deckp1.length;i++){ 
            this.tweens.add({
                targets: imm_deckp1[i],
                x: x+i*100,
                y: 500,
                delay:100+i*600,
                duration:600+i*40,
                ease: 'Linear',
            });
        }
        //<-----------------------------------INPUT SULLO SCARTO DELLA CARTA ------------------------------------->
        this.input.on('gameobjectdown', function (pointer, gameObject) {
            if(allowed == 0) return;
            if(gameObject.getData('state') == 'idle' && selectedCard != gameObject && selectedCard != null){
                this.tweens.add({
                    targets: selectedCard,
                    y: { value: y, duration: 10,}
                });
                this.tweens.add({
                    targets: gameObject,
                    y: { value: y-50, duration: 100, ease: 'Bounce.easeOut',}
                });
                selectedCard.setData('state','idle');
                gameObject.setData('state','focus');
                selectedCard = gameObject;
            }
            else if(gameObject.getData('state') == 'idle' && selectedCard == null){
                this.tweens.add({
                    targets: gameObject,
                    y: { value: y-50, duration: 100, ease: 'Bounce.easeOut',}
                });
                gameObject.setData('state','focus');
                selectedCard = gameObject;
                let scartabtn = this.add.image(0, 0, 'scarta_button').setOrigin(0).setInteractive();
                scartabtn.on("pointerup",()=>{
                    if(scarta_text == null){
                        scarta_text = this.add.text(300,300,"Sei vuoi scartare "+selectedCard.getData('name'));
                        si_text = this.add.text(300,350,"si").setInteractive();
                        si_text.on("pointerup",()=>{
                            this.tweens.add({
                                targets: selectedCard,
                                x: selectedCard.x,
                                y: selectedCard.y+200,
                                delay:50,
                                duration:600,
                                ease: 'Linear',
                                onComplete:() =>{
                                    remove(deckp1,imm_deckp1,selectedCard);
                                    //<-----------------------------------IA TI LEVA UNA CARTA ------------------------------------->
                                    iaremove(deckp1,imm_deckp1);
                                    for(var i = 0;i<imm_deckp1.length;i++){ 
                                        this.tweens.add({
                                            targets: imm_deckp1[i],
                                            x: 530,
                                            y: 100,
                                            delay:100+i*600,
                                            duration:600+3*40,
                                            ease: 'Linear',
                                        });
                                    }
                                    this.tweens.add({
                                        targets: retro,
                                        x: 530,
                                        y: 100,
                                        delay:100+5*600,
                                        duration:600+3*40,
                                        ease: 'Linear',
                                        onComplete:() =>{
                                            var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
                                            this.tweens.add({
                                                targets: r,
                                                alpha: { from: 0, to: 1 },
                                                duration:1000,
                                                onComplete:() =>{
                                                    for(var i = 0;i<imm_deckp1.length;i++){ 
                                                        imm_deckp1[i].setVisible(false);
                                                        imm_deckp1[i].setData('state','idle')
                                                    }
                                                    //setta gm
                                                    gameManager.player1.deckp1 = deckp1;
                                                    gameManager.player1.imm_deckp1 = imm_deckp1;
                                                    console.log(JSON.stringify(gameManager));
                                                    this.scene.start("Choose",{ gm : gameManager});
                                                }
                                            });
                                        }
                                    });
                                    allowed = 0;
                                }  
                            });
                            no_text.destroy();
                            si_text.destroy();
                            scarta_text.destroy();
                            scartabtn.destroy();
                        })
                        no_text = this.add.text(350,350,"no").setInteractive();
                    }
                    else{
                        scarta_text.destroy();
                        si_text.destroy();
                        no_text.destroy();
                        scarta_text = this.add.text(300,300,"Sei vuoi scartare "+selectedCard.getData('name'));
                        si_text = this.add.text(300,350,"si").setInteractive();
                        si_text.on("pointerup",()=>{
                            selectedCard.destroy();
                            no_text.destroy();
                            si_text.destroy();
                            scarta_text.destroy();
                            scartabtn.destroy();
                        })
                        no_text = this.add.text(350,350,"no").setInteractive();
                    }
                })
            }
        }, this);
    }
    update(){}
}

function remove(deck,i,sel){
    Phaser.Utils.Array.Remove(deck,sel.getData('name'));
    Phaser.Utils.Array.Remove(i,sel);
    sel.destroy();
}

function iaremove(deck,i) {
    var elem = Phaser.Utils.Array.RemoveRandomElement(i);
    Phaser.Utils.Array.Remove(deck,elem.getData('name'));
    Phaser.Utils.Array.Remove(i,elem);
    elem.destroy();
}

export default Select