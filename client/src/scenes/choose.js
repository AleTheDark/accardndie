import {Scene} from 'phaser'
class Choose extends Scene{
    constructor() {
        super("Choose");
    }
    init(data){
        this.gameManager = data.gm;
    }
    preload (){
        this.load.image('choose_bg','src/assets/choose_bg.png');
        this.load.atlas('retro','src/assets/cards.png','src/assets/retro.json');
        this.load.image('p1','src/assets/p1.png');
        this.load.image('p2','src/assets/p2.png');
        this.load.image('p3','src/assets/p3.png');
    }
    create(){
        var  gameManager = this.gameManager;
        var retro = this.add.image(530,100,'retro',frames_names_r).setOrigin(0).setDepth(1);
        var frames_names_r = this.textures.get('retro').getFrameNames();
        var p1 = this.add.image(320,180,'p1').setDepth(1).setInteractive();
        var p2 = this.add.image(600,180,'p2').setDepth(1).setInteractive();
        var p3 = this.add.image(880,180,'p3').setDepth(1).setInteractive();
        p1.on("pointerup",()=>{
            var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
            this.tweens.add({
                targets: r,
                alpha: { from: 0, to: 1 },
                duration:100,
                onComplete:() =>{
                    gameManager.room_type = Phaser.Math.Between(1,4+((Phaser.Math.Between(0, 2)*2)));
                    console.log(gameManager.room_type+" è la porcodio di stanza");
                    this.scene.start("Room",{gm:gameManager});
                }
            });
        });
        p2.on("pointerup",()=>{
            var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
            this.tweens.add({
                targets: r,
                alpha: { from: 0, to: 1 },
                duration:100,
                onComplete:() =>{
                    gameManager.room_type = Phaser.Math.Between(1,4+((Phaser.Math.Between(0, 2)*2)));
                    console.log(gameManager.room_type+" è la porcodio di stanza");
                    this.scene.start("Room",{gm:gameManager});
                }
            });        
        });
        p3.on("pointerup",()=>{
            var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
            this.tweens.add({
                targets: r,
                alpha: { from: 0, to: 1 },
                duration:100,
                onComplete:() =>{
                    gameManager.room_type = Phaser.Math.Between(1,4+((Phaser.Math.Between(0, 2)*2)));
                    console.log(gameManager.room_type+" è la porcodio di stanza");
                    this.scene.start("Room",{gm:gameManager});
                }
            });
        });
        this.add.text(400,0,"Choose_scene").setOrigin(0);
        this.add.image(1280/2,760/2,'choose_bg');
        this.tweens.add({
            targets: retro,
            x:1090,
            y:100,
            duration:100,    
        });
        var r = this.add.rectangle(1280/2, 760/2, 1280, 760, 0x000000,1);
        this.tweens.add({
            targets: r,
            alpha: { from: 1, to: 0 },
            duration:1000,
        });
    }
    upload (){}
}

function startRoll(){
    console.log("rollo");
    return Phaser.Math.Between(1,4+((Phaser.Math.Between(0, 2))*2));
}

export default Choose
