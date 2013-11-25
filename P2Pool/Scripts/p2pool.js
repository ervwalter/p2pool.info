/// <reference path="jquery-1.7.1.js" />
/// <reference path="store-json2.min.js" />
/// <reference path="knockout.js" />
/// <reference path="knockout.mapping-latest.js" />
/// <reference path="bootstrap.js" />
/// <reference path="highstock.src.js" />

/*jshint browser: true, jquery: true */
/*global ko: false, store: false, Highcharts: false*/

function ViewModel() {
    var self = this;
    var model = self;

    self.mappingBlock = {
        key: function (data) {
            return ko.utils.unwrapObservable(data.Id);
        },

        create: function (options) {
            var block = {};
            ko.mapping.fromJS(options.data, {}, block);

            block.When = ko.computed(function () {
                model.ticks();
                return new Date(block.Timestamp() * 1000).toRelativeTime(30000);
            });

            block.Time = ko.computed(function () {
                return Highcharts.dateFormat("%b %e, %Y, %l:%M:%S%P", block.Timestamp() * 1000);
            });

            block.DurationMarkup = ko.computed(function () {
                return model.formatDuration(block.RoundDuration());
            });

            block.PercentExpected = ko.computed(function () {
                var expected = block.ExpectedShares();
                var actual = block.ActualShares();
                if (expected <= 0) {
                    return "-";
                }
                var percent = (actual / expected * 100);
                return model.formatPercent(percent);
            });

            block.BlockUrl = ko.computed(function () {
                return 'http://blockchain.info/block-index/' + block.Id();
            });

            block.TxUrl = ko.computed(function () {
                return 'http://blockchain.info/tx/' + block.GenerationTxHash();
            });

            block.ShortTxHash = ko.computed(function () {
                return block.GenerationTxHash().substr(0, 30);
            });

            return block;
        }
    };

    self.mappingAddress = {
        key: function (data) {
            return ko.utils.unwrapObservable(data.Address);
        }
    };

    self.mappingDonation = {
        key: function (data) {
            return ko.utils.unwrapObservable(data.TxHash);
        },

        create: function (options) {
            var donation = {};
            ko.mapping.fromJS(options.data, {}, donation);

            donation.When = ko.computed(function () {
                model.ticks();
                return new Date(donation.Timestamp() * 1000).toRelativeTime(30000);
            });

            donation.Time = ko.computed(function () {
                return Highcharts.dateFormat("%b %e, %Y, %l:%M:%S%P", donation.Timestamp() * 1000);
            });

            donation.FormattedAmount = ko.computed(function () {
                return donation.Amount().toPrecisionString(4, 8);
            });

            donation.TxUrl = ko.computed(function () {
                return 'http://blockchain.info/tx/' + donation.TxHash();
            });

            donation.ShortTxHash = ko.computed(function () {
                return donation.TxHash();
            });

            return donation;
        }
    };

    self.blockCompare = function (left, right) {
        var rheight = right.BlockHeight();
        var lheight = left.BlockHeight();
        if (rheight == lheight) {
            var rtime = right.Timestamp();
            var ltime = left.Timestamp();
            return rtime == ltime ? 0 : (rtime < ltime ? -1 : 1);
        }
        return rheight < lheight ? -1 : 1;
    };

    //used to force updates to relative time UIs
    self.ticks = ko.observable(new Date().getTime() / 1000);

    //data loaded via ajax
    self.difficulty = ko.observable(0);
    self.hashrate = ko.observable(0);
    self.blocks = ko.mapping.fromJS([], self.mappingBlock);
    self.users = ko.mapping.fromJS([], self.mappingAddress);
    self.payouts = ko.mapping.fromJS([], self.mappingAddress);
    self.donations = ko.mapping.fromJS([], self.mappingDonation);

    //loaded flags
    self.statsLoaded = ko.observable(false);
    self.blocksLoaded = ko.observable(false);
    self.usersLoaded = ko.observable(false);
    self.payoutsLoaded = ko.observable(false);
    self.donationsLoaded = ko.observable(false);

    //settings
    self.playSound = ko.observable(true);
    self.starredAddresses = {};
    self.starChanged = ko.observable(new Date().getTime());

    self.canShowChart = true;
    self.dismissSvgAlert = false;

    self.estimatedTimeToBLock = ko.computed(function () {
        if (self.hashrate() === 0 || self.difficulty() === 0) {
            return "<b>loading...</b>";
        }

        var rate = self.hashrate() * 1000000000;
        var difficulty = self.difficulty();
        var time = difficulty * 4294967296 / rate;

        return self.formatDuration(time);
    });

    self.sortedBlocks = ko.computed(function () {
        return self.blocks.slice().sort(self.blockCompare);
    });

    self.currentRoundDuration = ko.computed(function () {
        var blocks = self.sortedBlocks();
        if (blocks.length > 0) {
            var newestBlockTimestamp = blocks[0].Timestamp();
            var difference = self.ticks() - newestBlockTimestamp;
            if (difference <= 0) {
                return '<div class="duration">0m</div>';
            }
            return self.formatDuration(difference);
        }
        return "loading...";
    });

    self.LastBlockHeight = ko.computed(function () {
        var blocks = self.sortedBlocks();
        if (blocks.length > 0) {
            return blocks[0].BlockHeight();
        }
        return 0;
    });

    self.poolLuck = function (cutoff) {
        var i, expected, actual, blocks, e, a, block, lastBlock;
        blocks = self.sortedBlocks();
        lastBlock = blocks[0];
        if (!lastBlock) {
            return "";
        }
        if (cutoff < 0) {
            cutoff = lastBlock.Timestamp() + cutoff;
        }
        expected = 0;
        actual = 0;
        for (i = 0; i < blocks.length; i++) {
            block = blocks[i];
            if (block.Timestamp() < cutoff) {
                break;
            }
            e = block.ExpectedShares();
            a = block.ActualShares();
            if (e > 0 && a > 0) {
                expected += e;
                actual += a;
            }
        }
        return 100 * expected / actual;
    };

    self.sevenDays = ko.computed(function () {
        return self.poolLuck(-604800); // 7 days
    });

    self.thirtyDays = ko.computed(function () {
        return self.poolLuck(-2592000); // 30 days
    });

    self.ninetyDays = ko.computed(function () {
        return self.poolLuck(-7776000); // 90 days
    });

    self.isStarred = function (data) {
        self.starChanged();
        var address = data.Address();
        return (self.starredAddresses[address] === true);
    };

    self.toggleStar = function (data) {
        var address = data.Address();
        if (address) {
            var starred = self.starredAddresses[address];
            if (starred) {
                delete self.starredAddresses[address];
            }
            else {
                self.starredAddresses[address] = true;
            }
            store.set("p2poolStarredAddresses", self.starredAddresses);
            self.starChanged(new Date().getTime());
        }
    };

    self.formatDuration = function (rawDuration) {
        if (rawDuration == 0) {
            return "-";
        }
        var duration = rawDuration;
        if (duration < 0) {
            duration = 0;
        }
        var days = Math.floor(duration / 86400);
        duration -= days * 86400;
        var hours = Math.floor(duration / 3600);
        duration -= hours * 3600;
        var minutes = Math.floor(duration / 60);
        var result = "";
        if (days > 0) {
            result += '<div class="duration">' + days + 'd</div>';
        }
        if (hours > 0) {
            result += '<div class="duration">' + hours + 'h</div>';
        }
        result += '<div class="duration">' + minutes + 'm</div>';
        return result;
    };

    self.formatPercent = function (percent) {
        if (percent < 0) {
            percent = 0;
        }
        return Highcharts.numberFormat(percent, 1) + '%';
    };

    self.skipFade = true;

    self.fadeIn = function (element) {
        if (self.skipFade) {
            $(element).show();
        }
        else {
            $(element).delay(1).fadeIn(1000);
        }
    };

}

$(function () {
    var chart;
    var latestRateTimestamp;
    var oldRateMax, oldUserMax;

    var model = new ViewModel();
    var complete = false;

    var starredAddresses = store.get("p2poolStarredAddresses");
    if (starredAddresses) {
        model.starredAddresses = starredAddresses;
    }

    var dismissSvgAlert = store.get("p2poolDismissSvgAlert");
    if (dismissSvgAlert) {
        model.dismissSvgAlert = dismissSvgAlert;
    }

    $("#alert-svg").click(function () {
        store.set("p2poolDismissSvgAlert", true);
    });

    var playSetting = store.get("p2poolPlaySound");
    if (typeof playSetting !== "undefined") {
        model.playSound(playSetting === true);
    }
    model.playSound.subscribe(function () {
        store.set("p2poolPlaySound", model.playSound());
    });

    if (!supportsSVG() && !supportsVml()) {
        model.canShowChart = false;
    }
    //setup the bindings
    ko.applyBindings(model);

    var blocksLoadedSubscription = model.blocksLoaded.subscribe(drawInitialBlockLines);
    var statsLoadedSubscription = model.statsLoaded.subscribe(drawInitialBlockLines);

    function drawInitialBlockLines() {
        if (model.blocksLoaded() && model.statsLoaded()) {
            blocksLoadedSubscription.dispose();
            statsLoadedSubscription.dispose();
            if (model.canShowChart) {
                var axis = chart.xAxis[0];
                var blocks = model.blocks();
                for (var i in blocks) {
                    axis.addPlotLine({ color: 'rgba(0,0,0,0.2)', width: 1, value: blocks[i].Timestamp() * 1000 });
                }
            }
        }
    }

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
            buttons: [{
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

    $.get("/stats", null, function (result) {

        var rates = result.rates;
        latestRateTimestamp = rates[rates.length - 1][0];
        oldRateMax = result.maxRate;
        oldUserMax = result.maxUsers;
        model.hashrate(rates[rates.length - 1][1]);

        if (model.canShowChart) {
            Highcharts.setOptions({
                global: {
                    useUTC: false
                }
            });

            var mainChartOptions = $.extend(true, {}, baseChartOptions, {
                chart: {
                    renderTo: 'chart'
                },
                tooltip: {
                    headerFormat: '<span>{point.key}</span><br/>'
                },

                rangeSelector: {
                    selected: 2
                },

                series: [{
                    id: 'rate',
                    name: 'Pool Hash Rate',
                    type: 'areaspline',
                    data: result.rates,
                    tooltip: {
                    	yDecimals: 0,
                        ySuffix: " GH/s"
                    },
                    animation: false
                }, {
                    id: 'users',
                    name: 'Active Users/Addresses',
                    type: 'spline',
                    data: result.users,
                    yAxis: 1,
                    animation: false,
                    dataGrouping: {
                        approximation: 'high'
                    }
                }, {
                    id: 'dummy',
                    name: 'Dummy Series',
                    type: 'line',
                    data: [[result.rates[0][0], 0]],
                    showInLegend: false,
                    animation: false,
                    yAxis: 1
                }],

                yAxis: [{
                    title: {
                        text: 'Hash Rate'
                    },
                    labels: {
                        x: -10,
                        y: 4,
                        align: 'right',
                        formatter: function () {
                        	if (this.value > 1000) {
                        		return (this.value / 1000) + " TH/s";
                        	} else {
                        		return (this.value) + " GH/s";
                        	}
                        }
                    }
                }, {
                    title: {
                        text: 'Users / Addresses',
                        offset: 40,
                        style: {
                            color: '#AA4643',
                            fontWeight: 'bold'
                        }
                    },
					min: 0,
                    opposite: true,
                    //maxPadding: 0.45,
                    labels: {
                        x: 30,
                        y: 4
                    }
                }]
            });

            chart = new Highcharts.StockChart(mainChartOptions);

        }

        model.statsLoaded(true);

        setInterval(function () {
            try {
                $.get("/stats?from=" + latestRateTimestamp, null, function (data) {
                    if (data.rates.length > 0) {
                        var rates = data.rates;
                        var users = data.users;
                        model.hashrate(rates[rates.length - 1][1]);
                        if (model.canShowChart) {
                            var seriesRate = chart.get('rate');
                            var seriesUsers = chart.get('users');
                            for (var i in rates) {
                                if (rates[i] && rates[i].length == 2) {
                                    seriesRate.addPoint(rates[i], false);
                                }
                            }
                            if (users.length > 0) {
                                for (var j in users) {
                                    if (users[j] && users[j].length == 2) {
                                        seriesUsers.addPoint(users[j], false);
                                    }
                                }
                            }
                            if (data.maxRate != oldRateMax) {
                                chart.yAxis[0].setExtremes(0, data.maxRate, false);
                                chart.yAxis[1].setExtremes(0, data.maxRate, false);
                                oldRateMax = data.maxRate;
                            }
                            chart.redraw();
                            latestRateTimestamp = data.rates[data.rates.length - 1][0];
                        }
                    }
                }, 'json');
            }
            catch (err) {
            }
        }, 120000);

        $.get("/blocks", null, function (data) {
            ko.mapping.fromJS(data, model.blocks);
            model.blocksLoaded(true);
            model.skipFade = false;
            setInterval(function () {
                try {
                    var from = model.LastBlockHeight();
                    $.get("/blocks?from=" + from, null, function (data) {
                        if (data.length > 0) {
                            if (model.playSound()) {
                                var audio = $("#audio-newblock")[0];
                                if (audio && audio.play) {
                                    audio.play();
                                }
                            }
                            var axis;
                            if (model.canShowChart) {
                                axis = chart.xAxis[0];
                            }
                            for (var i in data) {
                                if (model.canShowChart) {
                                    axis.addPlotLine({ color: 'rgba(0,0,0,0.2)', width: 1, value: data[i].Timestamp * 1000 });
                                }
                                model.blocks.unshift(ko.mapping.fromJS(data[i], model.mappingBlock));
                            }
                        }
                    }, 'json');
                }
                catch (err) {
                }
            }, 210000);

        }, 'json');


        function updatePayouts() {
            try {
                $.get("/payouts", null, function (data) {
                    model.payoutsLoaded(true);
                    ko.mapping.fromJS(data, model.payouts);
                }, 'json');
            }
            catch (err) {
            }
        }
        updatePayouts();
        setInterval(updatePayouts, 210000);

        function updateUsers() {
            try {
                $.get("/users", null, function (data) {
                    model.usersLoaded(true);
                    ko.mapping.fromJS(data, model.users);
                }, 'json');
            }
            catch (err) {
            }
        }
        updateUsers();
        setInterval(updateUsers, 210000);

        function updateDonations() {
            try {
                $.getJSON("/donations", null, function (data) {
                    model.donationsLoaded(true);
                    ko.mapping.fromJS(data, model.donations);
                });
            }
            catch (err) {
            }
        }
        updateDonations();
        setInterval(updateDonations, 999999);

        function updateDifficulty() {
            try {
                $.get("/difficulty", null, function (result) {
                    model.difficulty(result);
                }, 'json');
            }
            catch (err) {
            }
        }
        updateDifficulty();
        setInterval(updateDifficulty, 210000);

        //update ticks every 15 seconds.  this drives several time based UIs to update.
        setInterval(function () {
            try {
                model.ticks(new Date().getTime() / 1000);
            }
            catch (err) {
            }
        }, 15000);



    }, 'json');

    $(".helptip").popover();
});

Date.prototype.toRelativeTime = function (now_threshold) {
    var delta = new Date() - this;

    now_threshold = parseInt(now_threshold, 10);

    if (isNaN(now_threshold)) {
        now_threshold = 0;
    }

    if (delta <= now_threshold) {
        return 'Just now';
    }

    var units = null;
    var conversions = {
        millisecond: 1, // ms    -> ms
        second: 1000,   // ms    -> sec
        minute: 60,     // sec   -> min
        hour: 60,     // min   -> hour
        day: 24,     // hour  -> day
        month: 30,     // day   -> month (roughly)
        year: 12      // month -> year
    };

    for (var key in conversions) {
        if (delta < conversions[key]) {
            break;
        } else {
            units = key; // keeps track of the selected key over the iteration
            delta = delta / conversions[key];
        }
    }

    // pluralize a unit when the difference is greater than 1.
    delta = Math.floor(delta);
    if (delta !== 1) { units += "s"; }
    return [delta, units, "ago"].join(" ");
};

Date.fromString = function (str) {
    return new Date(Date.parse(str));
};

function supportsVml() {
    if (typeof supportsVml.supported == "undefined") {
        var a = document.body.appendChild(document.createElement('div'));
        a.innerHTML = '<v:shape id="vml_flag1" adj="1" />';
        var b = a.firstChild;
        b.style.behavior = "url(#default#VML)";
        supportsVml.supported = b ? typeof b.adj == "object" : true;
        a.parentNode.removeChild(a);
    }
    return supportsVml.supported;
}

function supportsSVG() {
    return !!document.createElementNS && !!document.createElementNS('http://www.w3.org/2000/svg', "svg").createSVGRect;
}

Number.prototype.toPrecisionString = function (minDigits, maxDigits) {
    var i, j, output, end;
    output = this.toFixed(maxDigits);
    end = output.length - 1;
    j = maxDigits - minDigits;
    for (i = 0; i < j; i++) {
        if (output.substr(end - i, 1) != "0") {
            break;
        }
    }
    return output.substr(0, (end - i) + 1);
};