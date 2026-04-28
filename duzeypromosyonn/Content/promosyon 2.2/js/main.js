$(document).ready(function(){
    $('#onecikarilan').owlCarousel({
        loop:false,
        margin:8,
        autoWidth:true,
        animateIn:true,
        autoplay:true,
        responsiveClass:true,
        nav: true,
        navText: ["<",">"],
        responsive:{
            0:{
                items:2
            },
            360:{
                items:2
            },
            300:{
                items:2.3
            },
            400:{
                items:2
            },
            500:{
                items:2.8
            },
            600:{
                items:3.2
            },
            700:{
                items:3
            },
            800:{
                items:4
            },
            900:{
                items:4,
                loop:false
            },
            1200:{
                items:4,
                loop:false
            },
            1400:{
                items:6,
                loop:false
            }
        }
    })
      
  });
  window.addEventListener('load', () => {
    AOS.init({
      duration: 1000,
      easing: 'ease-in-out',
      once: true,
      mirror: false
    })
  });
  $(window).scroll(function(){
    $('nav').toggleClass('scrolled',$(this).scrollTop()>50);
       
});
