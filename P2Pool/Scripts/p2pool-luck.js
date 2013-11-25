/// <reference path="jquery-1.7.1.js" />
/// <reference path="store-json2.min.js" />
/// <reference path="knockout.js" />
/// <reference path="knockout.mapping-latest.js" />
/// <reference path="bootstrap.js" />
/// <reference path="highstock.src.js" />

var chart;

function ViewModel() {
    var self = this;
    var model = self;

    self.mappingBlock = {
        key: function (data) {
            return ko.utils.unwrapObservable(data.Id);
        }
    };

    //data loaded via ajax
    self.blocks = [];
    self.stats = [];

    //loaded flags
    self.statsLoaded = ko.observable(false);
    self.blocksLoaded = ko.observable(false);

    //settings
    self.canShowChart = true;
    self.dismissSvgAlert = false;

    self.hashesInShares = [];
    self.hashesInBlocks = [];
    self.luckNinetyDays = [];
    self.luckThirtyDays = [];
    self.luckSevenDays = [];
    self.blockLines = [];

    self.processBlocks = function () {
        var i, timestamp,
        hashesBlocks = [],
        luckNinetyDays = [],
        luckThirtyDays = [],
        luckSevenDays = [],
        blockLines = [],
        ninetyExpected = 0,
        ninetyActual = 0,
        thirtyExpected = 0,
        thirtyActual = 0,
        sevenExpected = 0,
        sevenActual = 0,
        totalHashes = 0,
        blocks = self.blocks,
        max = blocks.length - 1,
        ninetyPos = max,
        thirtyPos = max,
        sevenPos = max,
        log10 = Math.log(10);

        for (i = max; i >= 0; i--) {
            timestamp = blocks[i].Timestamp;

            if (i == max) {
                continue;
            }

            ninetyExpected += blocks[i].ExpectedShares;
            ninetyActual += blocks[i].ActualShares;

            thirtyExpected += blocks[i].ExpectedShares;
            thirtyActual += blocks[i].ActualShares;

            sevenExpected += blocks[i].ExpectedShares;
            sevenActual += blocks[i].ActualShares;

            while (blocks[ninetyPos].Timestamp < (timestamp - 7776000)) {
                ninetyActual -= blocks[ninetyPos].ActualShares;
                ninetyExpected -= blocks[ninetyPos].ExpectedShares;
                ninetyPos--;
            }

            while (blocks[thirtyPos].Timestamp < (timestamp - 2592000)) {
                thirtyActual -= blocks[thirtyPos].ActualShares;
                thirtyExpected -= blocks[thirtyPos].ExpectedShares;
                thirtyPos--;
            }

            while (blocks[sevenPos].Timestamp < (timestamp - 604800)) {
                sevenActual -= blocks[sevenPos].ActualShares;
                sevenExpected -= blocks[sevenPos].ExpectedShares;
                sevenPos--;
            }

            luckNinetyDays.push([timestamp * 1000, 100 * ninetyExpected / ninetyActual]);
            luckThirtyDays.push([timestamp * 1000, 100 * thirtyExpected / thirtyActual]);
            luckSevenDays.push([timestamp * 1000, 100 * sevenExpected / sevenActual]);

        }

        self.luckNinetyDays = luckNinetyDays;
        self.luckThirtyDays = luckThirtyDays;
        self.luckSevenDays = luckSevenDays;
    };

    self.processStats = function () {
        var hashesInShares = [],
        hashesInBlocks = [],
        totalHashesInShares = 0,
        totalHashesInBlocks = 0,
        stats = self.stats,
        blocks = self.blocks,
        s = 1,
        b = blocks.length,
        timestamp = stats[s][0],
        hashrate = stats[s][1],
        lastTime = stats[0][0],
        nextBlockTime, nextStatsTime, timeDiff;

        while (true) {

            timeDiff = (timestamp - lastTime) / 1000;
            totalHashesInShares += timeDiff * hashrate * 0.000001;

            hashesInShares.push([timestamp, totalHashesInShares]);
            hashesInBlocks.push([timestamp, totalHashesInBlocks]);

            lastTime = timestamp;

            if (b > 0) {
                nextBlockTime = blocks[b - 1].Timestamp * 1000;
            } else {
                nextBlockTime = -1;
            }

            if (s < stats.length - 1) {
                nextStatsTime = stats[s + 1][0];
            } else {
                nextStatsTime = -1;
            }

            //quit looping if we have exhausted both arrays
            if (nextBlockTime < 0 && nextStatsTime < 0) {
                break;
            }

            //if the stats array is finished or if the block array is next or tied, advance the block array
            if (nextStatsTime < 0 || (nextBlockTime > 0 && nextBlockTime <= nextStatsTime)) {
                b--;
                timestamp = nextBlockTime;
                totalHashesInBlocks += blocks[b].Difficulty * 4294967296 * 0.000000000000001;
                if (nextStatsTime > 0) {
                    hashrate = stats[s + 1][1];
                }
            }

            if (nextBlockTime < 0 || (nextStatsTime > 0 && nextStatsTime <= nextBlockTime)) {
                s++;
                timestamp = nextStatsTime;
                hashrate = stats[s][1];
            }
        }

        self.hashesInBlocks = hashesInBlocks;
        self.hashesInShares = hashesInShares;

    };

}

$(function () {
    var model = new ViewModel();

    var dismissSvgAlert = store.get("p2poolDismissSvgAlert");
    if (dismissSvgAlert) {
        model.dismissSvgAlert = dismissSvgAlert;
    }

    $("#alert-svg").click(function () {
        store.set("p2poolDismissSvgAlert", true);
    });

    if (!supportsSVG() && !supportsVml()) {
        model.canShowChart = false;
    }
    //setup the bindings
    ko.applyBindings(model);

    var blocksLoadedSubscription = model.blocksLoaded.subscribe(blocksOrStatsLoaded);
    var statsLoadedSubscription = model.statsLoaded.subscribe(blocksOrStatsLoaded);

    function blocksOrStatsLoaded() {
        if (model.blocksLoaded() && model.statsLoaded()) {
            blocksLoadedSubscription.dispose();
            statsLoadedSubscription.dispose();

            //both sets of data are now loaded, draw the charts
            if (model.canShowChart) {

                //process data
                model.processStats();
                model.processBlocks();

                Highcharts.setOptions({
                    global: {
                        useUTC: false
                    }
                });

                var baseChartOptions = {
                    chart: {
                        spacingLeft: 0,
                        spacingRight: 0,
                        spacingTop: 20,
                        panning: false,
                        zoomType: 'x'
                    },
                    credits: {
                        enabled: false
                    },
                    legend: {
                        enabled: true,
                        y: -15,
                        x: -55,
                        floating: true,
                        verticalAlign: 'top',
                        align: 'right'
                    },
                    rangeSelector: {
                        enabled: true,
                        inputEnabled: false,
                        selected: 1,
                        buttons: [{
                            type: 'all',
                            text: 'All'
                        }, {
                            type: 'month',
                            count: 3,
                            text: '3m'
                        }, {
                            type: 'month',
                            count: 1,
                            text: '1m'
                        }, {
                            type: 'week',
                            count: 1,
                            text: '7d'
                        }, {
                            type: 'day',
                            count: 1,
                            text: '1d'
                        }]
                    },
                    plotOptions: {
                        series: {
                            dataGrouping: {
                                units: [['hour', [1, 2, 3, 4, 6, 8, 12]],
                    ['day', [1]],
                    ['week', [1]],
                    ['month', [1, 3, 6]],
                    ['year', null]],
                                dateTimeLabelFormats: {
                                    'millisecond': ['%A, %b %e, %l:%M:%S.%L%P', '%A, %b %e, %l:%M:%S.%L%P', '-%l:%M:%S.%L%P'],
                                    'second': ['%A, %b %e, %l:%M:%S%P', '%A, %b %e, %l:%M:%S%P', '-%l:%M:%S%P'],
                                    'minute': ['%A, %b %e, %l:%M%P', '%A, %b %e, %l:%M%P', '-%l:%M%P'],
                                    'hour': ['%A, %b %e, %l:%M%P', '%A, %b %e, %l:%M%P', '-%l:%M%P'],
                                    'day': ['%A, %b %e, %Y', '%A, %b %e', '-%A, %b %e, %Y'],
                                    'week': ['Week from %A, %b %e, %Y', '%A, %b %e', '-%A, %b %e, %Y'],
                                    'month': ['%B %Y', '%B', '-%B %Y'],
                                    'year': ['%Y', '%Y', '-%Y']
                                }
                            },
                            dateTimeLabelFormats: {
                                second: '%l:%M:%S%P',
                                minute: '%l:%M%P',
                                hour: '%l%P',
                                day: '%b %e',
                                week: '%b %e',
                                month: '%b \'%y',
                                year: '%Y'
                            }
                        }
                    },
                    xAxis: {
                        ordinal: false,
                        tickPixelInterval: 200,
                        dateTimeLabelFormats: {
                            second: '%l:%M:%S%P',
                            minute: '%l:%M%P',
                            hour: '%l%P',
                            day: '%b %e',
                            week: '%b %e',
                            month: '%b \'%y',
                            year: '%Y'
                        }
                    }
                };

                var luckChartsBaseOptions = $.extend(true, {}, baseChartOptions, {
                    chart: {
                        spacingLeft: 20,
                        spacingRight: 20,
                        spacingTop: 20,
                        spacingBottom: 60,
                        borderWidth: 1,
                        borderColor: '#cccccc'
                    },
                    tooltip: {
                        enabled: true,
                        headerFormat: '<span>{point.key}</span><br/>'
                    },


                    plotOptions: {
                        series: {
                            states: {
                                hover: {
                                    enabled: false
                                }
                            }
                        }
                    },
                    legend: {
                        enabled: true,
                        y: 0,
                        x: 0,
                        floating: true,
                        verticalAlign: 'top',
                        align: 'right',
                        layout: 'vertical'
                    }
                });

                var luckBlocksVsHashesOptions = $.extend(true, {}, luckChartsBaseOptions, {
                    chart: {
                        renderTo: 'chart-blocksvshashes'
                    },
                    colors: [
	                    '#AA4643',
	                    '#89A54E',
	                    '#80699B',
	                    '#4572A7',
	                    '#3D96AE',
	                    '#DB843D',
	                    '#92A8CD',
	                    '#DDDD00',
	                    '#A47D7C'
                    ],
                    yAxis: {
                        title: {
                            text: 'Hashes'
                        },
                        startOnTick: true,
                        endOnTick: true,
                        showLastLabel: true,
                        minPadding: 0,
                        labels: {
                            x: -10,
                            y: 4,
                            align: 'right',
                            formatter: function () {
                                return (this.value) + " PH";
                            }
                        }
                    },
                    title: {
                        text: 'Hashing Performed vs. Blocks Produced'
                    },
                    tooltip: {
                        formatter: function () {
                            var s = null, blocks, shares;

                            try {

                                s = '' + Highcharts.dateFormat('%A, %b %e, %Y', this.x) + '';

                                for (var i = 0; i < this.points.length; i++) {
                                    s += '<br/><span style="color: ' + this.points[i].series.color + ';">' + this.points[i].series.name + ':</span> <b>' + Highcharts.numberFormat(this.points[i].y, 1) + ' PH</b>';
                                }

                                if (this.points.length == 2) {
                                    s += '<br/><span style="color: #000000;">Cumulative Luck:</span> <b>' + Highcharts.numberFormat(100 * this.points[1].y / this.points[0].y, 1) + '%</b>';
                                }

                            } catch (e) {
                                return false;
                            }
                            return s;
                        }
                    },
                    series: [{
                        id: 'hashes',
                        name: 'Cumulative Hashes in Shares',
                        type: 'line',
                        lineWidth: 1,
                        data: model.hashesInShares,
                        tooltip: {
                            yDecimals: 0,
                            ySuffix: " PH"
                        },
                        dataGrouping: {
                            approximation: 'high'
                        },
                        turboThreshold: 10,
                        animation: false
                    }, {
                        id: 'blocks',
                        name: 'Cumulative Hashes in Blocks',
                        type: 'area',
                        lineWidth: 1,
                        fillOpacity: 0.4,
                        step: true,
                        threshold: null,
                        data: model.hashesInBlocks,
                        tooltip: {
                            yDecimals: 0,
                            ySuffix: " PH"
                        },
                        dataGrouping: {
                            approximation: 'high'
                        },
                        turboThreshold: 10,
                        animation: false
                    }]

                });
                var luckBlocksVsHashesChart = new Highcharts.StockChart(luckBlocksVsHashesOptions);
                chart = luckBlocksVsHashesChart;

                //no earlier than Feb 1, 2012
                var maxRange = new Date().getTime() - 1328097600000;

                var luckOverTimeOptions = $.extend(true, {}, luckChartsBaseOptions, {
                    chart: {
                        renderTo: 'chart-luckovertime',
                        zoomType: ''
                    },
                    yAxis: {
                        title: {
                            text: 'Luck'
                        },
                        showLastLabel: true,
                        startOnTick: true,
                        endOnTick: true,
                        gridZIndex: -1,
                        gridLineDashStyle: 'dot',
                        plotLines: [{
                            color: '#A0A0A0',
                            value: 100,
                            width: 1,
                            zIndex: 0
                        }],
                        labels: {
                            x: -10,
                            y: 4,
                            align: 'right',
                            formatter: function () {
                                return this.value + " %";
                            }
                        }
                    },
                    xAxis: {
                        range: Math.min(maxRange, 90 * 3600 * 24 * 1000),
                        tickInterval: 7 * 3600 * 24 * 1000,
                        minorTickInterval: 1 * 3600 * 24 * 1000,
                        minorGridLineWidth: 0,
                        startOfWeek: 0
                    },
                    navigator: {
                        enabled: false
                    },
                    scrollbar: {
                        enabled: false
                    },
                    rangeSelector: {
                        enabled: false
                    },
                    title: {
                        text: 'Pool Luck Over Time'
                    },
                    legend: {
                        reversed: true
                    },
                    tooltip: {
                        formatter: function () {
                            var s = null, blocks, shares;

                            try {

                                s = '' + Highcharts.dateFormat('%A, %b %e, %Y', this.x) + '';


                                for (var i = this.points.length - 1; i >= 0; i--) {
                                    s += '<br/><span style="color: ' + this.points[i].series.color + ';">' + this.points[i].series.name + ':</span> <b>' + Highcharts.numberFormat(this.points[i].y, 1) + '%</b>';
                                }

                            } catch (e) {
                                return false;
                            }
                            return s;
                        }
                    },
                    series: [
                    {
                        id: 'alltime',
                        enableMouseTracking: true,
                        name: 'Luck, 90 Day Moving Average',
                        type: 'line',
                        marker: {
                            enabled: false,
                            symbol: 'circle',
                            radius: 4
                        },
                        lineWidth: 1,
                        data: model.luckNinetyDays,
                        tooltip: {
                            yDecimals: 1,
                            ySuffix: "%"
                        },
                        turboThreshold: 10,
                        animation: false
                    },
                    {
                        id: 'thirty',
                        enableMouseTracking: true,
                        name: 'Luck, 30 Day Moving Average',
                        type: 'line',
                        marker: {
                            enabled: false,
                            symbol: 'circle',
                            radius: 4
                        },
                        lineWidth: 1,
                        data: model.luckThirtyDays,
                        tooltip: {
                            yDecimals: 1,
                            ySuffix: "%"
                        },
                        turboThreshold: 10,
                        animation: false
                    },
                    {
                        id: 'alltime',
                        enableMouseTracking: true,
                        name: 'Luck, 7 Day Moving Average',
                        type: 'line',
                        marker: {
                            enabled: false,
                            symbol: 'circle',
                            radius: 4
                        },
                        lineWidth: 1,
                        data: model.luckSevenDays,
                        tooltip: {
                            yDecimals: 1,
                            ySuffix: "%"
                        },
                        turboThreshold: 10,
                        animation: false
                    }
                    ]

                });
                var luckOverTimeChart = new Highcharts.StockChart(luckOverTimeOptions);


            }
        }
    }

    $.getJSON("/stats", null, function (result) {
        model.stats = result.rates;
        model.statsLoaded(true);
    });

    $.getJSON("/blocks?all=true", null, function (data) {
        model.blocks = data;
        model.blocksLoaded(true);
    });

    $(".helptip").popover();
});


function supportsVml() {
    if (typeof supportsVml.supported == "undefined") {
        var a = document.body.appendChild(document.createElement('div'));
        a.innerHTML = '<v:shape id="vml_flag1" adj="1" />';
        var b = a.firstChild;
        b.style.behavior = "url(#default#VML)";
        supportsVml.supported = b ? typeof b.adj == "object" : true;
        a.parentNode.removeChild(a);
    }
    return supportsVml.supported
}

function supportsSVG() {
    return !!document.createElementNS && !!document.createElementNS('http://www.w3.org/2000/svg', "svg").createSVGRect;
}