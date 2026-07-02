import {Scene} from 'phaser'

class Start extends Scene{
    constructor() {
        super("Start");
    }

    init (){}
    preload (){
        this.load.atlas('retro','src/assets/cards.png','src/assets/retro.json')
        
    }
    create(){

        this.add.text(400,0,"Start_Scene").setOrigin(0);
     
        this.scene.start("Select");
    }
    upload (){}

}
    
export default Start