import Phaser from 'phaser';
import Start from './scenes/start'
import Choose from './scenes/choose'
import Select from './scenes/select'
import Room from './scenes/room'
import Settings from './helpers/settings'


const config = {
    type: Phaser.AUTO,
    parent: 'game',
    width: 1280,
    height: 760,
    backgroundColor: "#000000",
    scene: [Start,Select,Choose,Room]
};

const game = new Phaser.Game(config);
